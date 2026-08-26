namespace ATS.Services.BulkSubmissionProcessor;

public class BulkSubmissionProcessorService : IBulkSubmissionProcessorService
{
	private readonly IATSRepository _repository;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly IObjectStorageService _objectStorageService;
	private readonly ISecureToken _secureToken;
	private readonly IHashService _hashService;
	private readonly IHubContext<ATSHub, IATSClient> _hubContext;
	private readonly ILogger<BulkSubmissionProcessorService> _logger;
	private readonly IConfiguration _configuration;
	private readonly int _applicationFormExpiryInHours;

	// Comfortably longer than a full parse pass so a live worker is never robbed of
	// files it is still processing.
	private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(30);

	public BulkSubmissionProcessorService(
		IATSRepository repository,
		IServiceScopeFactory serviceScopeFactory,
		IObjectStorageService objectStorageService,
		ISecureToken secureToken,
		IHashService hashService,
		IHubContext<ATSHub, IATSClient> hubContext,
		ILogger<BulkSubmissionProcessorService> logger,
		IConfiguration configuration)
	{
		_repository = repository;
		_serviceScopeFactory = serviceScopeFactory;
		_objectStorageService = objectStorageService;
		_secureToken = secureToken;
		_hashService = hashService;
		_hubContext = hubContext;
		_logger = logger;
		_configuration = configuration;
		_applicationFormExpiryInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
	}

	public async Task ProcessAsync(CancellationToken cancellationToken)
	{
		// A crash mid-parse leaves files claimed as Processing with no live worker, so
		// release anything stale before claiming the next batch.
		var released = await _repository.ReleaseStaleBulkFileClaimsAsync(StaleClaimTimeout);

		if (released > 0)
		{
			_logger.LogWarning(
				"Released {ReleasedCount} stale bulk file claim(s) back to Pending.",
				released);
		}

		// The claim atomically moves a batch of Pending files to Processing, so a
		// concurrent worker cannot parse the same CSV.
		var pendingFiles = await _repository.GetBulkUploadFileDetailsAsync();

		if (!pendingFiles.Any())
			return;

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
						BulkFileID = file.FileID,
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
						ClientId = file.ClientId,
						RequestorId = file.UploadedByUserId,
						Requestor = file.Requestor,
						OrderCreatedAt = DateTime.UtcNow,

						// Bulk orders are auto-ticketed on the same terms as single
						// enrolments; the ticketing job claims them from this status.
						TicketStatus = TicketStatus.Pending,
						IsTicketed = false
					});
				}

				await scopedRepository.AddBulkEmailInvitationRequestAsync(subjects);

				await _hubContext
						.Clients
						.Group(file.UploadedByUserId.ToString()!)
						.ReceiveATSResponse($"Your bulk upload \"{file.FileName}\" has been received and is now being processed.");

				return (file, succeeded: true);
			}
			catch (Exception ex)
			{
				// Leave the file Pending so the next tick retries it. The catch is here
				// so one failing file does not stop its siblings from being marked Done,
				// which would otherwise re-insert their candidates on the next tick.
				_logger.LogError(ex, "Failed Transaction: Bulk file processing failed, will retry: {@Context}", logContext);

				return (file, succeeded: false);
			}
			finally
			{
				semaphore.Release();
			}
		});

		var results = await Task.WhenAll(tasks);

		// Only files that actually inserted their invitation rows are marked Done; the
		// rest are released back to Pending and picked up again on the next tick.
		var processedFiles = results
			.Where(r => r.succeeded)
			.Select(r => r.file)
			.ToList();

		var failedFiles = results
			.Where(r => !r.succeeded)
			.Select(r => r.file)
			.ToList();

		if (processedFiles.Count > 0)
		{
			// The invitation rows are already persisted with EmailSentStatus = Pending,
			// so the email notification job picks them up straight from PostgreSQL.
			var processedFileIds = processedFiles.Select(file => file.FileID).ToList();

			await _repository.UpdateBulkFileDetailsStatusAsync(processedFileIds, BulkFileStatus.Done);
		}

		if (failedFiles.Count > 0)
		{
			// Release the claim now instead of leaving these files to the sweeper.
			await _repository.ReleaseBulkFileClaimsAsync(failedFiles);
		}
	}
}
