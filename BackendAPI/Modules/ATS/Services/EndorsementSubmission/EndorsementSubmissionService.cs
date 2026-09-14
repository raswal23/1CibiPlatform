namespace ATS.Services.EndorsementSubmission;

public class EndorsementSubmissionService : IEndorsementSubmissionService
{
	private readonly ILogger<EndorsementSubmissionService> _logger;
	private readonly IHashService _hashService;
	private readonly IEmailService _emailService;
	private readonly HybridCache _hybridCache;
	private readonly IConfiguration _configuration;
	private readonly ISecureToken _secureToken;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IATSRepository _atsRepository;
	private readonly IObjectStorageService _objectStorageService;
	private readonly ICurrentUser _currentUser;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly IUserClientRepository _userClientRepository;
	private readonly IAtsAccessScopeResolver _accessScopeResolver;
	private readonly IOrderInputValidator _orderInputValidator;
	private readonly IUnitOfWork _unitOfWork;
	private readonly string _templateFileName;
	private readonly string _applicationformBaseUrl;
	private readonly int _applicationFormExpiryInHours;
	private readonly string _folderName;

	public EndorsementSubmissionService(
		ILogger<EndorsementSubmissionService> logger,
		IATSRepository atsRepository,
		IConfiguration configuration,
		IHashService hashService,
		[FromKeyedServices("ats")] IEmailService emailService,
		HybridCache hybridCache,
		ISecureToken secureToken,
		IHttpContextAccessor httpContextAccessor,
		ICurrentUser currentUser,
		IObjectStorageService objectStorageService,
		IOrderHistoryService orderHistoryService,
		IUserClientRepository userClientRepository,
		IAtsAccessScopeResolver accessScopeResolver,
		IOrderInputValidator orderInputValidator,
		IUnitOfWork unitOfWork)
	{
		_logger = logger;
		_hashService = hashService;
		_emailService = emailService;
		_hybridCache = hybridCache;
		_secureToken = secureToken;
		_httpContextAccessor = httpContextAccessor;
		_configuration = configuration;
		_atsRepository = atsRepository;
		_objectStorageService = objectStorageService;
		_currentUser = currentUser;
		_orderHistoryService = orderHistoryService;
		_userClientRepository = userClientRepository;
		_accessScopeResolver = accessScopeResolver;
		_orderInputValidator = orderInputValidator;
		_unitOfWork = unitOfWork;
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
		_templateFileName = _configuration.GetSection("ATS").GetValue<string>("ATSBulkTemplatePath") ?? string.Empty;
		_applicationFormExpiryInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
		_folderName = _configuration.GetSection("ATS").GetValue<string>("ATSBulkFileFolderName", "");
	}

	public async Task<string> GetBulkTemplateFileUrlAsync()
	{
		return await _hybridCache.GetOrCreateAsync(
			"bulk-template-url",
			async _ =>
			{
				return await _objectStorageService.GenerateDownloadUrlAsync(
					_templateFileName,
					TimeSpan.FromMinutes(15));
			},
			options: new HybridCacheEntryOptions
			{
				Expiration = TimeSpan.FromMinutes(14)
			});
	}

	public async Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default, string source = OrderHistorySource.Web)
	{
		var subjectName = $"{emailInvitationRequestDTO.FirstName} {emailInvitationRequestDTO.LastName}";

		var logContext = new
		{
			Action = "InsertData",
			Step = "StartInserting",
			Identity = subjectName,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Inserting Email invitaion request {@Context}", logContext);

		// Before anything is created: the package must be one this client is assigned
		// and the order type must be Rush or Normal. Both used to be length-checked
		// only, so any text was accepted and the mistake surfaced much later at OMS
		// ticketing. Throws BadRequestException naming the acceptable values.
		var validated = await _orderInputValidator.ValidateAsync(
			emailInvitationRequestDTO.SelectPackage,
			emailInvitationRequestDTO.RushNormal,
			ct);

		// The web console sends the screening type the user chose, and the package
		// list it chose from is filtered by that type - so a mismatch means a stale
		// or tampered request. Public API and assistant callers send null and skip
		// the check; the package's own classification stands.
		if (emailInvitationRequestDTO.AutoChasing is not null
			&& validated.AutoChasing != emailInvitationRequestDTO.AutoChasing)
		{
			throw new BadRequestException("The selected package does not match the chosen screening type.");
		}

		// Written back so the caller is echoed what was actually stored - a request
		// sending "rush" gets "Rush" - and so the Adapt below carries the resolved id
		// and canonical spelling onto the entity. AutoChasing is snapshotted from the
		// package (not the caller) so the order keeps the classification it was
		// placed under even if the package is reclassified later.
		emailInvitationRequestDTO.PackageId = validated.PackageId;
		emailInvitationRequestDTO.SelectPackage = validated.Package;
		emailInvitationRequestDTO.RushNormal = validated.OrderType;
		emailInvitationRequestDTO.AutoChasing = validated.AutoChasing;

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

		EmailInvitationRequest emailInvitationRequest = emailInvitationRequestDTO.Adapt<EmailInvitationRequest>();
		emailInvitationRequest.EmailInvitationID = Guid.CreateVersion7();

		// Handed back on the DTO so an API caller can poll the order they just created.
		emailInvitationRequestDTO.OrderId = emailInvitationRequest.EmailInvitationID;
		emailInvitationRequest.HashToken = HashToken;
		emailInvitationRequest.HashTokenCreatedAt = DateTime.UtcNow;
		emailInvitationRequest.OrderCreatedAt = DateTime.UtcNow;
		emailInvitationRequest.EmailSentStatus = EmailStatus.Pending;
		emailInvitationRequest.ApplicationFormStatus = ApplicationFormStatus.Pending;
		emailInvitationRequest.OrderStatus = OrderStatus.PendingCandidateInfo;

		// Queues the order for OMS auto-ticketing. The background job claims it from
		// here; there is no outbox, the status column is the queue.
		emailInvitationRequest.TicketStatus = TicketStatus.Pending;
		emailInvitationRequest.IsTicketed = false;
		emailInvitationRequest.RequestorId = _currentUser.UserId;
		emailInvitationRequest.ClientId = _currentUser.AtsClientId;
		emailInvitationRequest.Requestor = _currentUser.FullName;
		emailInvitationRequest.HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);

		var applicationFormLink = $"{_applicationformBaseUrl}/{HashToken}";

		// The insert, the email and the status update are one unit.
		//
		// They used to be three separate steps, and AddEmailInvitationRequestAsync calls
		// SaveChangesAsync itself - so the order was committed before the email was even
		// attempted. A failed send left a saved order whose candidate never received a
		// link: invisible until somebody noticed the form was never filled in.
		//
		// The send is inside the transaction, so failing it rolls the order back and the
		// caller gets a clean failure to retry rather than a half-created order. What that
		// does NOT cover: SMTP is external and cannot be rolled back, so if the send
		// succeeds and the commit then fails, the candidate holds a link to an order that
		// no longer exists. That window is the price of sending inline; the alternative is
		// queueing it for EmailNotificationProcessor, which is how bulk orders work.
		//
		// TransactionRunner owns the begin / SaveChanges / commit / rollback, and rethrows
		// untouched so CustomExceptionHandler still decides the status code.
		await TransactionRunner.RunAsync(
			_unitOfWork,
			async () =>
			{
				await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

				await SendApplicationFormToUserEmailAsync(
					emailInvitationRequestDTO.EmailAddress!,
					subjectName,
					applicationFormLink,
					emailInvitationRequest.Requestor,
					emailInvitationRequest.ClientId);

				await _atsRepository.UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(
					emailInvitationRequest.EmailInvitationID);

				await _orderHistoryService.RecordAsync(
					emailInvitationRequest.EmailInvitationID,
					OrderHistoryEventType.OrderCreated,
					null,
					OrderStatus.PendingCandidateInfo, ct, source);
			},
			ct);

		return true;
	}
	// TryUpdateEmailStatusToNotSentAsync was removed with its only caller. It existed to
	// mark a saved order's email as "Not Sent" after an inline send failed - which only
	// made sense while the order survived that failure. The send now runs inside the
	// transaction, so a failure takes the order with it and there is no row left to mark.

	public async Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO, CancellationToken ct = default, string source = OrderHistorySource.Web)
	{
		string bulkFileKey = "";
		bulkUploadFileDetailsDTO.UploadedByUserId = Guid.Parse(_httpContextAccessor!.HttpContext!
		   .User
		   .FindFirst(ClaimTypes.NameIdentifier)!
		   .Value);


		var logContext = new
		{
			Action = "UploadFile",
			Step = "StartUploading",
			Identity = bulkUploadFileDetailsDTO.UploadedByUserId,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Starting uploading process for file {FileName}", bulkUploadFileDetailsDTO.FileName);

		if (await BulkUploadFileNameExistsAsync(bulkUploadFileDetailsDTO.FileName!, ct))
		{
			throw new BadRequestException("A file with this name has already been uploaded.");
		}

		// Validated before the file is stored: the package and order type apply to every
		// row, so an unassigned package would fail the whole upload once parsed. Better
		// to reject it here than to accept a file that cannot produce a single order.
		var validated = await _orderInputValidator.ValidateAsync(
			bulkUploadFileDetailsDTO.PackageType,
			bulkUploadFileDetailsDTO.OrderType,
			ct);

		// Same rule as the single path: the console's chosen screening type must
		// agree with the package's own classification. Null (public API) skips it.
		if (bulkUploadFileDetailsDTO.AutoChasing is not null
			&& validated.AutoChasing != bulkUploadFileDetailsDTO.AutoChasing)
		{
			throw new BadRequestException("The selected package does not match the chosen screening type.");
		}

		bulkUploadFileDetailsDTO.PackageId = validated.PackageId;
		bulkUploadFileDetailsDTO.PackageType = validated.Package;
		bulkUploadFileDetailsDTO.OrderType = validated.OrderType;
		// Snapshotted from the package, not the caller, exactly as on single orders.
		bulkUploadFileDetailsDTO.AutoChasing = validated.AutoChasing;


		if (bulkUploadFileDetailsDTO.BulkFile != null)
		{
			await using var fileStream = bulkUploadFileDetailsDTO.BulkFile.OpenReadStream();

			bulkFileKey = await _objectStorageService.UploadAsync(
				_folderName,
				bulkUploadFileDetailsDTO.FileName!,
				fileStream,
				ct);
		}
		BulkUploadFileDetails bulkUploadFileDetails = bulkUploadFileDetailsDTO.Adapt<BulkUploadFileDetails>();
		bulkUploadFileDetails.FileID = Guid.CreateVersion7();

		// Handed back on the DTO so an API caller can poll this file's parse outcome.
		bulkUploadFileDetailsDTO.FileId = bulkUploadFileDetails.FileID;
		bulkUploadFileDetails.Status = BulkFileStatus.Pending;
		bulkUploadFileDetails.DateCreated = DateTime.UtcNow;
		// Captured here, not in the parsing job: that job runs on a Quartz thread with no
		// HttpContext, so ICurrentUser would resolve to null for every row it creates.
		bulkUploadFileDetails.ClientId = _currentUser.AtsClientId;
		bulkUploadFileDetails.UploadedByUserId = _currentUser.UserId;
		bulkUploadFileDetails.Requestor = _currentUser.FullName;
		bulkUploadFileDetails.FileKey = bulkFileKey;

		// Carried on the file so the parsing job can stamp it on every order it creates.
		bulkUploadFileDetails.Source = source;

		// The file is already in object storage by this point, and storage is not part of
		// any database transaction - so if the row insert fails, the blob has to be deleted
		// by hand or it is orphaned there forever. RunWithCompensationAsync owns that
		// pattern and rethrows the insert failure untouched.
		await TransactionRunner.RunWithCompensationAsync(
			work: () => _atsRepository.AddBulkUploadFileDetailsAsync(bulkUploadFileDetails),
			compensate: () => _objectStorageService.DeleteAsync(bulkFileKey, ct),
			onCompensationFailed: exception => _logger.LogError(
				exception,
				"Failed to delete the orphaned bulk upload {FileKey} after its row insert failed.",
				bulkFileKey));

		_logger.LogInformation(
			"Successfully added the file info in the database and object storage - {FileID}: {@Context}",
			bulkUploadFileDetailsDTO.UploadedByUserId,
			logContext);

		return true;
	}

	public Task<bool> BulkUploadFileNameExistsAsync(string fileName, CancellationToken ct = default) =>
		_atsRepository.BulkUploadFileNameExistsAsync(
			fileName,
			_currentUser.AtsClientId,
			_currentUser.UserId,
			ct);

	public async Task<IReadOnlyList<int>> GetInvalidBulkMobileNumberRowsAsync(IFormFile file, CancellationToken ct = default)
	{
		return await ATS.Features.Web.InsertBulkSubject.BulkMobileNumberValidation.ValidateMobileNumbersAsync(file, ct);
	}

	public async Task<bool> SendApplicationFormToUserEmailAsync(string gmail, string name, string applicationFormLink, string? requestor, int? clientId)
	{
		var logContext = new
		{
			Action = "SendApplicationFormEmail",
			Step = "SendEmail",
			Email = gmail,
			Timestamp = DateTime.UtcNow
		};

		var result = await SendApplicationFormToUserEmailWithResultAsync(
			gmail,
			name,
			applicationFormLink,
			requestor,
			clientId,
			CancellationToken.None);

		if (!result.IsSent)
		{
			_logger.LogError("Failed to send Notification email to: {@Context}", logContext);

			// The single-order paths run inside a transaction: a failed send must take the
			// order with it rather than leaving a saved order whose candidate never got a
			// link. The bulk job calls the result overload instead, precisely so it can
			// keep the row and retry it.
			throw new InternalServerException("Failed to send Notification email.");
		}

		return true;
	}

	public async Task<EmailDeliveryResult> SendApplicationFormToUserEmailWithResultAsync(
		string gmail,
		string name,
		string applicationFormLink,
		string? requestor,
		int? clientId,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "SendApplicationFormEmail",
			Step = "SendEmail",
			Email = gmail,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending notification for email: {@Context}", logContext);

		var clientName = await ResolveClientNameAsync(clientId);

		var emailBody = _emailService.SendAppplicationFormNotification(gmail, name, applicationFormLink, requestor, clientName);

		// The keyed "ats" registration is always ATSEmailService, which implements the
		// result-aware contract. The cast is guarded rather than assumed so a future
		// re-registration degrades to the bool path instead of throwing at runtime.
		if (_emailService is IAtsEmailSender resultAwareSender)
		{
			return await resultAwareSender.SendATSEmailWithResultAsync(
				toEmail: gmail!,
				subject: "CIBI | Background Verification Information Request",
				body: emailBody,
				cancellationToken);
		}

		var isSent = await _emailService.SendATSEmailAsync(
			toEmail: gmail!,
			subject: "CIBI | Background Verification Information Request",
			body: emailBody);

		return isSent
			? EmailDeliveryResult.Sent
			: EmailDeliveryResult.Transient(null, "Email sender reported failure without a status code.");
	}

	// A missing or unknown client id degrades to null - the email body falls back to
	// a generic phrasing instead of blocking the send.
	private async Task<string?> ResolveClientNameAsync(int? clientId)
	{
		if (!clientId.HasValue)
			return null;

		// Purely cosmetic: the name is interpolated into the email body, and a null falls
		// back to generic phrasing. Failing to read it must never fail the send, so it goes
		// through SideEffectGuard rather than a local catch.
		return await SideEffectGuard.RunAsync(
			async () =>
			{
				var clients = await _atsRepository.GetClientsByIdsAsync(
					[clientId.Value], searchTerm: null, CancellationToken.None);

				return clients.FirstOrDefault()?.ClientName;
			},
			_logger,
			$"resolve client name for client {clientId} (the email falls back to generic phrasing)");
	}

	public async Task<KeysetPaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetWithdrawnApplicationForm",
			Step = "FetchingWithdrawnApplicationForm",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching withdrawn application form with pagination: {@Context}", logContext);

		// The role ladder lives in AtsAccessScopeResolver now - this used to be an
		// inline copy of it.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			return CreateEmptyWithdrawnResult(paginationRequest);
		}

		var clientIds = scope.AuthorizedClientIds;
		var requiredRequestorId = scope.RequiredOwnerId;

		// Cursor over the fixed (createdAt?, id) ordering. An empty createdAt keeps
		// legacy rows with a null timestamp pageable.
		var fields = CursorCodec.Decode(paginationRequest.Cursor, 2);
		Guid? afterId = Guid.TryParse(fields?[1], out var invitationId) ? invitationId : null;
		DateTime? afterCreatedAt = afterId.HasValue
			&& DateTime.TryParse(fields![0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt)
			? createdAt : null;
		var pageSize = KeysetPage.Clamp(paginationRequest.PageSize);

		var rows = await _atsRepository.GetWithdrawnPageAsync(
			paginationRequest.SearchTerm, afterCreatedAt, afterId, pageSize + 1,
			clientIds, requiredRequestorId, cancellationToken);
		var (items, hasMore) = KeysetPage.Trim(rows, pageSize);

		var nextCursor = hasMore
			? CursorCodec.Encode(
				items[^1].OrderCreatedAt?.ToString("O"),
				items[^1].EmailInvitationID.ToString("D"))
			: null;
		long? totalCount = afterId.HasValue
			? null
			: await _atsRepository.CountWithdrawnAsync(
				paginationRequest.SearchTerm, clientIds, requiredRequestorId, cancellationToken);

		return new KeysetPaginatedResult<EmailInvitationRequestListDTO>(items, nextCursor, totalCount);
	}

	private static KeysetPaginatedResult<EmailInvitationRequestListDTO> CreateEmptyWithdrawnResult(
		KeysetPaginationRequest paginationRequest) =>
		new(
			Array.Empty<EmailInvitationRequestListDTO>(), null, 0);

	public async Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "ResendApplicationForm",
			Step = "FetchingRecord",
			EmailInvitationId = emailInvitationId,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Resending application form for invitation: {@Context}", logContext);

		var invitation = await _atsRepository.GetEmailInvitationRequestByIdAsync(emailInvitationId, cancellationToken);

		if (invitation.EmailInvitationID == Guid.Empty)
		{
			_logger.LogError("Failed to find email invitation for resend: {@Context}", logContext);
			throw new NotFoundException($"Email invitation with ID {emailInvitationId} not found.");
		}

		// Resend is reachable from more than one screen and takes a caller-supplied id,
		// so the caller's client/requestor scope is enforced here rather than relying on
		// the calling page to only offer ids it already listed. Out of scope reads as
		// not found: the response must not reveal that the invitation exists.
		if (!await IsInvitationWithinCallerScopeAsync(invitation, cancellationToken))
		{
			_logger.LogWarning("Resend denied for out-of-scope invitation: {@Context}", logContext);
			throw new NotFoundException($"Email invitation with ID {emailInvitationId} not found.");
		}

		var token = _secureToken.GenerateSecureToken();
		if (string.IsNullOrEmpty(token))
		{
			_logger.LogError("Failed to generate new token: {@Context}", logContext);
			throw new InternalServerException("Failed to generate new token.");
		}

		var hashToken = _hashService.Hash(token);
		if (string.IsNullOrEmpty(hashToken))
		{
			_logger.LogError("Failed to hash token: {@Context}", logContext);
			throw new InternalServerException("Failed to hash token.");
		}

		var newExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);

		// Same shape as the create path above: the new token, the email and the history
		// entry are one unit, so a failed send does not leave the candidate holding a link
		// whose token was never issued - or an issued token nobody received.
		await TransactionRunner.RunAsync(
			_unitOfWork,
			async () =>
			{
				await _atsRepository.ResendApplicationFormAsync(emailInvitationId, hashToken, newExpiration, cancellationToken);

				var applicationFormLink = $"{_applicationformBaseUrl}/{hashToken}";
				var fullName = $"{invitation.FirstName} {invitation.LastName}";

				await SendApplicationFormToUserEmailAsync(
					invitation.EmailAddress!,
					fullName,
					applicationFormLink,
					invitation.Requestor,
					invitation.ClientId);

				await _orderHistoryService.RecordAsync(
					emailInvitationId,
					OrderHistoryEventType.ApplicationFormResent,
					invitation.OrderStatus,
					OrderStatus.PendingCandidateInfo,
					cancellationToken);
			},
			cancellationToken);

		_logger.LogInformation("Successfully resent application form for invitation: {@Context}", logContext);

		return true;
	}

	// Applies the same role ladder the read paths use. A null scope means the caller may
	// not read ATS records at all; a null AuthorizedClientIds means super admin.
	private async Task<bool> IsInvitationWithinCallerScopeAsync(
		EmailInvitationRequest invitation,
		CancellationToken cancellationToken)
	{
		var scope = await _accessScopeResolver.ResolveAsync(cancellationToken);

		if (scope is not { } accessScope)
		{
			return false;
		}

		if (accessScope.AuthorizedClientIds is { } clientIds
			&& !(invitation.ClientId.HasValue && clientIds.Contains(invitation.ClientId.Value)))
		{
			return false;
		}

		return !accessScope.RequiredOwnerId.HasValue
			|| invitation.RequestorId == accessScope.RequiredOwnerId.Value;
	}
}
