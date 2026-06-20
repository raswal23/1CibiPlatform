namespace ATS.BackgroundServices;

public class BulkSubmissionBackgroundService : BackgroundService
{
	private readonly ILogger<BulkSubmissionBackgroundService> _logger;
	private readonly IHubContext<ATSHub, IATSClient> _hubContext;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IConfiguration _configuration;
	private readonly IConnectionMultiplexer _redis;
	private readonly HybridCache _hybridCache;
	private readonly int _applicationFormExpiryInHours;
	private readonly string _batchesPending;

	public BulkSubmissionBackgroundService(IServiceScopeFactory scopeFactory,
										   IConfiguration configuration,
										   IConnectionMultiplexer redis,
										   ILogger<BulkSubmissionBackgroundService> logger,
										   IHubContext<ATSHub, IATSClient> hubContext,
										   HybridCache hybridCache)
	{
		_scopeFactory = scopeFactory;
		_configuration = configuration;
		_redis = redis;
		_logger = logger;
		_hubContext = hubContext;
		_batchesPending = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesPending") ?? string.Empty;
		_applicationFormExpiryInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
		_hybridCache = hybridCache;
	}
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			var dbRedis = _redis.GetDatabase();

			using var scope = _scopeFactory.CreateScope();

			var globalRepository = scope.ServiceProvider
				.GetRequiredService<IATSRepository>();

			var uploadFiles = scope.ServiceProvider
				.GetRequiredService<IObjectStorageService>();

			var secureToken = scope.ServiceProvider
				.GetRequiredService<ISecureToken>();

			var hashToken = scope.ServiceProvider
				.GetRequiredService<IHashService>();

			var pendingFiles =
				await globalRepository.GetBulkUploadFileDetailsAsync();

			if (!pendingFiles.Any())
			{
				await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
				continue;
			}

			var semaphore = new SemaphoreSlim(3);

			var tasks = pendingFiles.Select(async file =>
			{
				var logContext = new
				{
					Action = "BulkInsert",
					Step = "StartBulkInserting",
					Identity = file.FileID,
					Timestamp = DateTime.UtcNow
				};

				await semaphore.WaitAsync(stoppingToken);

				try
				{
					using var scope = _scopeFactory.CreateScope();

					var scopedRepository = scope.ServiceProvider
						.GetRequiredService<IATSRepository>();

					List<EmailInvitationRequest> subjects = new();

					await using var stream =
						await uploadFiles.DownloadAsync(file.FileKey!, stoppingToken);

					using var reader = new StreamReader(stream);

					using var csv = new CsvReader(
						reader,
						CultureInfo.InvariantCulture);

					var records = csv.GetRecords<BulkUploadCsvRecord>();

					foreach (var row in records)
					{
						var token = secureToken.GenerateSecureToken();

						if (string.IsNullOrEmpty(token))
						{
							_logger.LogError("Failed Transaction: Failed to generate Token for identity: {@Context}", logContext);
							throw new InternalServerException("Failed to generate Token.");
						}

						var HashToken = hashToken.Hash(token);

						if (string.IsNullOrEmpty(HashToken))
						{
							_logger.LogError("Failed Transaction: Failed to hash Token for identity: {@Context}", logContext);
							throw new InternalServerException("Failed to hash Token.");
						}

						subjects.Add(new EmailInvitationRequest
						{
							EmailInvitationID = Guid.CreateVersion7(),
							HashToken = HashToken,
							HashTokenCreated = DateTime.UtcNow,
							HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours),
							LastName = row.LastName,
							FirstName = row.FirstName,
							MiddleInitial = row.MiddleInitial,
							EmailAddress = row.EmailAddress,
							MobileNumber = row.MobileNumber,
							SelectPackage = file.PackageType,
							Status = "Pending",
							RushNormal = file.OrderType
						});
					}

					await scopedRepository.AddBulkEmailInvitationRequestAsync(subjects);

					await _hubContext
							.Clients
							.Group(file.UploadedByUserId.ToString())
							.ReceiveATSResponse($"Your bulk upload \"{file.FileName}\" has been received and is now being processed.");

					return subjects;
				}
				finally
				{
					semaphore.Release();
				}
			});

			List<EmailInvitationRequest>[] results = await Task.WhenAll(tasks);

			await globalRepository.UpdateBulkFileDetailsStatusAsync(pendingFiles);

			var listOfListOfSubjects = results.ToList();

			var batchId = $"batch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

			await _hybridCache.SetAsync(
					batchId,
					listOfListOfSubjects,
					new HybridCacheEntryOptions
					{
						Expiration = TimeSpan.FromMinutes(30)
					});

			await dbRedis.ListRightPushAsync(_batchesPending, batchId);

			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}
	}
}
