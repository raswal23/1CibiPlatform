namespace ATS.Services;

public class BulkSubmissionProcessorService : IBulkSubmissionProcessorService
{
	private readonly IATSRepository _repository;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly IObjectStorageService _objectStorageService;
	private readonly ISecureToken _secureToken;
	private readonly IHashService _hashService;
	private readonly IConnectionMultiplexer _redis;
	private readonly IHubContext<ATSHub, IATSClient> _hubContext;
	private readonly ILogger<BulkSubmissionProcessorService> _logger;
	private readonly ICurrentUser _currentUser;
	private readonly IConfiguration _configuration;
	private readonly int _applicationFormExpiryInHours;
	private readonly string _batchesPending;

	public BulkSubmissionProcessorService(
		IATSRepository repository,
		IServiceScopeFactory serviceScopeFactory,
		IObjectStorageService objectStorageService,
		ISecureToken secureToken,
		IHashService hashService,
		IConnectionMultiplexer redis,
		IHubContext<ATSHub, IATSClient> hubContext,
		ILogger<BulkSubmissionProcessorService> logger,
		ICurrentUser currentUser,
		IConfiguration configuration)
	{
		_repository = repository;
		_serviceScopeFactory = serviceScopeFactory;
		_objectStorageService = objectStorageService;
		_secureToken = secureToken;
		_hashService = hashService;
		_redis = redis;
		_hubContext = hubContext;
		_logger = logger;
		_currentUser = currentUser;
		_configuration = configuration;
		_batchesPending = _configuration.GetSection("CacheKeys").GetValue<string>("ATSBatchesPending") ?? string.Empty;
		_applicationFormExpiryInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
	}

	public async Task ProcessAsync(CancellationToken cancellationToken)
	{
		var dbRedis = _redis.GetDatabase();

		var pendingFiles = await _repository.GetBulkUploadFileDetailsAsync();

		if (!pendingFiles.Any())
			return;

		var pendingFileIds = pendingFiles.Select(file => file.FileID).ToList();

		await _repository.UpdateBulkFileDetailsStatusAsync(pendingFileIds, OrderStatus.InProgress);

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

			await semaphore.WaitAsync(cancellationToken);

			try
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedRepository = scope.ServiceProvider.GetRequiredService<IATSRepository>();

				List<EmailInvitationRequest> subjects = new();

				await using var stream = await _objectStorageService.DownloadAsync(file.FileKey!, cancellationToken);

				using var reader = new StreamReader(stream);

				using var csv = new CsvReader(
					reader,
					CultureInfo.InvariantCulture);

				if (!csv.Read() || !csv.ReadHeader())
				{
					_logger.LogError("Failed Transaction: Invalid CSV header for identity: {@Context}", logContext);
					throw new InternalServerException("Invalid CSV format. Missing header row.");
				}

				var expectedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				{
					nameof(BulkUploadCsvRecord.LastName),
					nameof(BulkUploadCsvRecord.FirstName),
					nameof(BulkUploadCsvRecord.MiddleInitial),
					nameof(BulkUploadCsvRecord.EmailAddress),
					nameof(BulkUploadCsvRecord.MobileNumber)
				};

				var actualHeaders = csv.HeaderRecord?
					.Select(header => header?.Trim())
					.Where(header => !string.IsNullOrWhiteSpace(header))
					.Select(header => header!)
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
					?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				if (!expectedHeaders.SetEquals(actualHeaders))
				{
					_logger.LogError("Failed Transaction: Invalid CSV columns for identity: {@Context}. Expected: {ExpectedHeaders}. Actual: {ActualHeaders}", logContext, string.Join(",", expectedHeaders), string.Join(",", actualHeaders));
					throw new InternalServerException("Invalid CSV format. Please use the required column headers.");
				}

				var records = csv.GetRecords<BulkUploadCsvRecord>();

				foreach (var row in records)
				{
					var token = _secureToken.GenerateSecureToken();

					if (string.IsNullOrEmpty(token))
					{
						_logger.LogError("Failed Transaction: Failed to generate Token for identity: {@Context}", logContext);
						throw new InternalServerException("Failed to generate Token.");
					}

					var HashToken = _hashService.Hash(token);

					if (string.IsNullOrEmpty(HashToken))
					{
						_logger.LogError("Failed Transaction: Failed to hash Token for identity: {@Context}", logContext);
						throw new InternalServerException("Failed to hash Token.");
					}

					subjects.Add(new EmailInvitationRequest
					{
						EmailInvitationID = Guid.CreateVersion7(),
						HashToken = HashToken,
						HashTokenCreatedAt = DateTime.UtcNow,
						HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours),
						LastName = row.LastName,
						FirstName = row.FirstName,
						MiddleInitial = row.MiddleInitial,
						EmailAddress = row.EmailAddress,
						MobileNumber = row.MobileNumber,
						SelectPackage = file.PackageType,
						EmailSentStatus = EmailStatus.Pending,
						ApplicationFormStatus = ApplicationFormStatus.Pending,
						OrderStatus = OrderStatus.PendingCandidateInfo,
						RushNormal = file.OrderType,
						ClientId = _currentUser.AtsClientId,
						RequestorId = _currentUser.UserId,
						Requestor = _currentUser.FullName,
						OrderCreatedAt = DateTime.UtcNow
					});
				}

				await scopedRepository.AddBulkEmailInvitationRequestAsync(subjects);

				await _hubContext
						.Clients
						.Group(file.UploadedByUserId.ToString()!)
						.ReceiveATSResponse($"Your bulk upload \"{file.FileName}\" has been received and is now being processed.");

				return subjects;
			}
			finally
			{
				semaphore.Release();
			}
		});

		List<EmailInvitationRequest>[] results = await Task.WhenAll(tasks);

		await _repository.UpdateBulkFileDetailsStatusAsync(pendingFileIds, OrderStatus.InProgress);

		var listOfListOfSubjects = results.ToList();

		var batchId = $"batch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

		// Payload must outlive the recovery job's stale threshold (1 day) so a
		// requeued batch can still be reprocessed; hence Redis string, not HybridCache.
		await dbRedis.StringSetAsync(
				batchId,
				JsonSerializer.Serialize(listOfListOfSubjects),
				TimeSpan.FromDays(2));

		await dbRedis.SortedSetAddAsync(
				_batchesPending,
				batchId,
				DateTimeOffset.UtcNow.ToUnixTimeSeconds());
	}
}
