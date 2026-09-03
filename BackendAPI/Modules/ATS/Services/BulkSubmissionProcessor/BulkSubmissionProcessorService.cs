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

	// The uploader is told what actually happened. Silently reporting "received" when
	// rows were dropped is how a bad column goes unnoticed until the orders never arrive.
	private static string BuildUploadReceivedMessage(string? fileName, int acceptedCount, int rejectedCount)
	{
		if (rejectedCount == 0)
		{
			return $"Your bulk upload \"{fileName}\" has been received and is now being processed.";
		}

		return $"Your bulk upload \"{fileName}\" created {acceptedCount} order(s). "
			+ $"{rejectedCount} row(s) were skipped because they were incomplete or invalid.";
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
				List<BulkUploadRejectedRowDTO> rejectedRows = new();

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

				var expectedHeaders = new List<string>
				{
					nameof(BulkUploadCsvRecord.LastName),
					nameof(BulkUploadCsvRecord.FirstName),
					nameof(BulkUploadCsvRecord.MiddleInitial),
					nameof(BulkUploadCsvRecord.EmailAddress),
					nameof(BulkUploadCsvRecord.MobileNumber)
				};

				var actualHeaders = csv.HeaderRecord?
					.Select(header => header?.Trim() ?? string.Empty)
					.ToList()
					?? [];

				// The template's sequence is the standard: the file must LEAD with the
				// expected columns in exactly this order. Columns AFTER them are
				// spreadsheet debris (notes, helper formulas) - CsvHelper maps by header
				// name and never reads them, so they are harmless, and the preview
				// dialog drops them for the same reason.
				var headersMatchTemplate =
					actualHeaders.Count >= expectedHeaders.Count
					&& expectedHeaders
						.Select((expected, index) =>
							string.Equals(actualHeaders[index], expected, StringComparison.OrdinalIgnoreCase))
						.All(matches => matches);

				if (!headersMatchTemplate)
				{
					_logger.LogError("Failed Transaction: Invalid CSV columns for identity: {@Context}. Expected: {ExpectedHeaders}. Actual: {ActualHeaders}", logContext, string.Join(",", expectedHeaders), string.Join(",", actualHeaders));
					throw new InternalServerException("Invalid CSV format. Please use the required column headers.");
				}

				var records = csv.GetRecords<BulkUploadCsvRecord>();

				// Row numbers are 1-based over the data rows, matching what the uploader
				// sees in their spreadsheet once the header is discounted.
				var rowNumber = 0;

				foreach (var row in records)
				{
					rowNumber++;

					// One unusable row must not reject the file: the good rows are still
					// worth creating, and the bad ones are reported back instead of being
					// inserted to fail later at email send or OMS ticketing.
					var (rejectionReason, mobileNumber) = BulkSubjectRowValidator.Validate(row);

					if (rejectionReason is not null)
					{
						rejectedRows.Add(new BulkUploadRejectedRowDTO
						{
							RowNumber = rowNumber,
							Reason = rejectionReason
						});

						continue;
					}

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
						// Optional column: a candidate may have no middle initial. Blank is
						// stored as null, matching how EditSubjectName persists it.
						MiddleInitial = string.IsNullOrWhiteSpace(row.MiddleInitial)
							? null
							: row.MiddleInitial.Trim(),
						EmailAddress = row.EmailAddress,

						// The normalised local form, so every stored number reads the
						// same regardless of how the CSV wrote it.
						MobileNumber = mobileNumber,
						// Both carried from the file: the id is the relationship, the
						// name the label, exactly as on a single order.
						PackageId = file.PackageId,
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

				// Bulk orders previously recorded no history at all, so their timelines
				// started blank while single orders showed OrderCreated. The source is
				// taken from the file because this job has no HTTP context to resolve
				// the caller from.
				if (subjects.Count > 0)
				{
					await scope.ServiceProvider
						.GetRequiredService<IOrderHistoryService>()
						.RecordManyAsync(
							subjects.Select(subject => subject.EmailInvitationID).ToList(),
							OrderHistoryEventType.OrderCreated,
							null,
							OrderStatus.PendingCandidateInfo,
							cancellationToken,
							file.Source ?? OrderHistorySource.Web,
							file.UploadedByUserId);
				}

				// Recorded on the file so the uploader can read back which rows were
				// refused and why; the upload response returned long before this ran.
				await scopedRepository.RecordBulkFileRowOutcomeAsync(
					file.FileID,
					subjects.Count,
					rejectedRows,
					cancellationToken);

				if (rejectedRows.Count > 0)
				{
					_logger.LogWarning(
						"Bulk file {FileID} had {RejectedCount} unusable row(s) of {TotalCount}: {@Context}",
						file.FileID,
						rejectedRows.Count,
						subjects.Count + rejectedRows.Count,
						logContext);
				}

				await _hubContext
						.Clients
						.Group(file.UploadedByUserId.ToString()!)
						.ReceiveATSResponse(BuildUploadReceivedMessage(file.FileName, subjects.Count, rejectedRows.Count));

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
