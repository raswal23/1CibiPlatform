namespace ATS.Services;

public class EmailNotificationRecoveryService : IEmailNotificationRecoveryService
{
	private readonly ILogger<EmailNotificationRecoveryService> _logger;
	private readonly IConnectionMultiplexer _redis;
	private readonly string _batchesPending;
	private readonly string _batchesProcessing;
	private readonly int _stuckThresholdHours;

	public EmailNotificationRecoveryService(
		ILogger<EmailNotificationRecoveryService> logger,
		IConfiguration configuration,
		IConnectionMultiplexer redis)
	{
		_logger = logger;
		_redis = redis;
		_batchesPending = configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesPending") ?? string.Empty;
		_batchesProcessing = configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesProcessing") ?? string.Empty;
		_stuckThresholdHours = configuration.GetSection("ATS").GetValue<int?>("ATSBatchStuckThresholdHours") ?? 24;
	}

	public async Task RequeueStaleBatchesAsync(CancellationToken cancellationToken)
	{
		var dbRedis = _redis.GetDatabase();

		var cutoff = DateTimeOffset.UtcNow.AddHours(-_stuckThresholdHours).ToUnixTimeSeconds();

		RedisValue[] staleBatches;

		try
		{
			staleBatches = await dbRedis.SortedSetRangeByScoreAsync(
				_batchesProcessing,
				double.NegativeInfinity,
				cutoff);
		}
		catch (RedisTimeoutException ex)
		{
			_logger.LogWarning(ex, "Redis timeout while reading {_batchesProcessing}", _batchesProcessing);

			return;
		}

		if (staleBatches.Length == 0)
		{
			return;
		}

		var requeuedCount = 0;

		foreach (var batchId in staleBatches)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				// ZADD before ZREM: a crash between the two leaves the entry in both
				// sets (the claim script's ZADD simply refreshes it), never in neither.
				await dbRedis.SortedSetAddAsync(
					_batchesPending,
					batchId,
					DateTimeOffset.UtcNow.ToUnixTimeSeconds());

				await dbRedis.SortedSetRemoveAsync(_batchesProcessing, batchId);

				requeuedCount++;
			}
			catch (RedisTimeoutException ex)
			{
				_logger.LogWarning(ex, "Redis timeout while requeueing stale batch {BatchId}", (string?)batchId);
			}
		}

		_logger.LogInformation(
			"Requeued {RequeuedCount} stale batch(es) older than {ThresholdHours}h from {Processing} back to {Pending}.",
			requeuedCount,
			_stuckThresholdHours,
			_batchesProcessing,
			_batchesPending);
	}
}
