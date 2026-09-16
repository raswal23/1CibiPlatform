namespace ATS.Services.EndorsementSubmission;

public class EndorsementSubmissionService : IEndorsementSubmissionService
{
	// A bulk resend is bounded because every requeued invitation becomes a message on the
	// deliberately-paced email queue. At the default 0.9 sends/second, 500 invitations is
	// already about nine minutes of sending; releasing thousands at once would block every
	// other client behind one operator's click.
	public const int MaxBulkResendSize = 500;

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

		// Manual screening is the only type that gets an application form. A data order
		// already carries the candidate's identity from order entry, so there is nothing
		// to ask them for - and every email column stays NULL rather than Pending.
		// Pending would be a lie in two directions: it tells a requestor an invitation is
		// on its way, and it describes a queue position this row does not hold, since the
		// worker claims "AutoChasing" IS TRUE and would never advance it.
		var sendsApplicationForm = emailInvitationRequest.AutoChasing is true;

		emailInvitationRequest.EmailSentStatus = sendsApplicationForm ? EmailStatus.Pending : null;
		emailInvitationRequest.ApplicationFormStatus = ApplicationFormStatus.Pending;
		emailInvitationRequest.OrderStatus = OrderStatus.PendingCandidateInfo;

		// Queues the order for OMS auto-ticketing. The background job claims it from
		// here; there is no outbox, the status column is the queue.
		emailInvitationRequest.TicketStatus = TicketStatus.Pending;
		emailInvitationRequest.IsTicketed = false;
		emailInvitationRequest.RequestorId = _currentUser.UserId;
		emailInvitationRequest.ClientId = _currentUser.AtsClientId;
		emailInvitationRequest.Requestor = _currentUser.FullName;

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
		// A data order skips the send and the status update entirely, so its transaction is
		// just the insert and the history entry. It is still queued for OMS ticketing - only
		// the candidate-facing email is suppressed, not the order itself.
		//
		// TransactionRunner owns the begin / SaveChanges / commit / rollback, and rethrows
		// untouched so CustomExceptionHandler still decides the status code.
		await TransactionRunner.RunAsync(
			_unitOfWork,
			async () =>
			{
				await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

				if (sendsApplicationForm)
				{
					await SendApplicationFormToUserEmailAsync(
						emailInvitationRequestDTO.EmailAddress!,
						subjectName,
						applicationFormLink,
						emailInvitationRequest.Requestor,
						emailInvitationRequest.ClientId);

					await _atsRepository.UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(
						emailInvitationRequest.EmailInvitationID);
				}

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

	private const string InvitationSubject = "CIBI | Background Verification Information Request";
	private const string ReminderSubject = "CIBI | Reminder: Background Verification Information Request";

	public async Task<EmailDeliveryResult> SendApplicationFormToUserEmailWithResultAsync(
		string gmail,
		string name,
		string applicationFormLink,
		string? requestor,
		int? clientId,
		CancellationToken cancellationToken,
		bool isFollowUp = false)
	{
		var logContext = new
		{
			Action = isFollowUp ? "SendApplicationFormReminderEmail" : "SendApplicationFormEmail",
			Step = "SendEmail",
			Email = gmail,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending notification for email: {@Context}", logContext);

		var clientName = await ResolveClientNameAsync(clientId);

		// The keyed "ats" registration is always ATSEmailService, which implements the
		// result-aware contract. The cast is guarded rather than assumed so a future
		// re-registration degrades to the bool path instead of throwing at runtime.
		//
		// The reminder body lives on IAtsEmailSender rather than the shared IEmailService,
		// which Auth and the test fakes also implement - see that interface's own note. A
		// sender that is not the ATS one therefore falls back to the first-invitation body:
		// the candidate still gets a working link, just without the reminder wording.
		var resultAwareSender = _emailService as IAtsEmailSender;

		var emailBody = isFollowUp && resultAwareSender is not null
			? resultAwareSender.BuildApplicationFormReminderNotification(gmail, name, applicationFormLink, requestor, clientName)
			: _emailService.SendAppplicationFormNotification(gmail, name, applicationFormLink, requestor, clientName);

		var subject = isFollowUp ? ReminderSubject : InvitationSubject;

		if (resultAwareSender is not null)
		{
			return await resultAwareSender.SendATSEmailWithResultAsync(
				toEmail: gmail!,
				subject: subject,
				body: emailBody,
				cancellationToken);
		}

		var isSent = await _emailService.SendATSEmailAsync(
			toEmail: gmail!,
			subject: subject,
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

		// A data order was deliberately never emailed, so there is nothing to resend -
		// and doing it would deliver the application form the screening type exists to
		// avoid. The dialog already hides the button; this takes a caller-supplied id, so
		// the rule is enforced where it cannot be skipped by calling the endpoint directly.
		if (invitation.AutoChasing is not true)
		{
			_logger.LogWarning("Resend denied for a non-manual invitation: {@Context}", logContext);
			throw new BadRequestException(
				"This order does not use manual screening, so no application form is sent to the candidate.");
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

		// Queued, not sent inline - the same strategy the OMS ticketing retry uses.
		//
		// Sending here meant the resend bypassed the connection pool and the rate limiter
		// entirely: it opened its own SMTP session on the request thread, which is exactly
		// the per-message login that got this sender throttled. It also left the row's
		// EmailSentStatus and EmailSendAttempts untouched, so a SUCCESSFUL resend still read
		// "Error, 5 attempts" in the dashboard, and the bulk-completion notification could
		// never fire for that file.
		//
		// The requeue moves the row back to Pending with a fresh token and a reset budget,
		// and the background job delivers it through the paced, pooled path like any other
		// invitation.
		var requeued = await _atsRepository.RequeueEmailInvitationAsync(
			emailInvitationId,
			hashToken,
			cancellationToken);

		// The button was stale: the row is not in a state a retry applies to. Say so rather
		// than reporting a silent success, matching RetryTicketAsync.
		if (!requeued)
		{
			_logger.LogWarning("Resend rejected, the invitation is no longer retryable: {@Context}", logContext);

			throw new ConflictException(
				"This invitation is no longer awaiting a resend. Refresh the list to see its current status.");
		}

		// After the requeue committed, so the history reflects work that is actually
		// scheduled. The order's own status is unchanged - queueing an email is not a step
		// in the order lifecycle - so it is written on both sides of the entry.
		await _orderHistoryService.RecordAsync(
			emailInvitationId,
			OrderHistoryEventType.ApplicationFormResent,
			invitation.OrderStatus,
			OrderStatus.PendingCandidateInfo,
			cancellationToken);

		_logger.LogInformation("Queued an application form resend for invitation: {@Context}", logContext);

		return true;
	}

	public async Task<BulkRetryResultDTO> ResendApplicationFormsAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "ResendApplicationForms",
			Step = "RequeueInvitations",
			RequestedCount = emailInvitationIds.Count,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation(
			"Queueing {Count} application form resend(s): {@Context}",
			emailInvitationIds.Count,
			logContext);

		// Distinct because a selection can repeat an id, and a duplicate would be counted
		// twice in the total reported back.
		var requestedIds = emailInvitationIds.Distinct().ToList();

		if (requestedIds.Count > MaxBulkResendSize)
		{
			throw new BadRequestException(
				$"A bulk resend is limited to {MaxBulkResendSize} invitations at a time. Narrow the selection and try again.");
		}

		var scope = await _accessScopeResolver.ResolveAsync(cancellationToken);

		if (scope is not { } accessScope)
		{
			throw new ForbiddenException("The current user does not have ATS access.");
		}

		var owners = await _atsRepository.GetEmailInvitationOwnersAsync(requestedIds, cancellationToken);

		// Scope is enforced per row, not once for the request: without this a caller could
		// touch another client's invitations by posting their ids alongside their own.
		// Out-of-scope ids are dropped silently, for the same reason the single resend
		// answers 404 - naming them would confirm the invitations exist.
		var inScopeIds = owners
			.Where(owner => IsOwnerWithinScope(owner, accessScope))
			.Select(owner => owner.EmailInvitationID)
			.ToList();

		if (inScopeIds.Count == 0)
		{
			throw new NotFoundException("None of the selected invitations are available to resend.");
		}

		// Each invitation gets its OWN token. Reusing one across the batch would let any
		// candidate in it open another candidate's application form.
		var requeues = new List<EmailInvitationRequeueDTO>(inScopeIds.Count);

		foreach (var invitationId in inScopeIds)
		{
			var token = _secureToken.GenerateSecureToken();

			if (string.IsNullOrEmpty(token))
			{
				_logger.LogError("Failed to generate new token during bulk resend: {@Context}", logContext);
				throw new InternalServerException("Failed to generate new token.");
			}

			var hashToken = _hashService.Hash(token);

			if (string.IsNullOrEmpty(hashToken))
			{
				_logger.LogError("Failed to hash token during bulk resend: {@Context}", logContext);
				throw new InternalServerException("Failed to hash token.");
			}

			requeues.Add(new EmailInvitationRequeueDTO
			{
				EmailInvitationId = invitationId,
				HashToken = hashToken
			});
		}

		var requeued = await _atsRepository.RequeueEmailInvitationsAsync(requeues, cancellationToken);

		// Recorded for the rows the caller was allowed to act on. A row skipped because the
		// job is mid-send keeps its own history from that send, so no entry is lost.
		if (requeued > 0)
		{
			await _orderHistoryService.RecordManyAsync(
				inScopeIds,
				OrderHistoryEventType.ApplicationFormResent,
				null,
				OrderStatus.PendingCandidateInfo,
				cancellationToken);
		}

		_logger.LogInformation(
			"Queued {RequeuedCount} of {RequestedCount} application form resend(s): {@Context}",
			requeued,
			requestedIds.Count,
			logContext);

		return new BulkRetryResultDTO
		{
			RequestedCount = requestedIds.Count,
			RequeuedCount = requeued
		};
	}

	public async Task<int> ReleaseDueFollowUpEmailsAsync(CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "ReleaseDueFollowUpEmails",
			Step = "ReleaseInvitations",
			Timestamp = DateTime.UtcNow
		};

		// One statement does the whole release: it picks the due rows, moves them back to
		// Pending and stamps FollowUpQueuedAt together, so a crash cannot leave a row
		// requeued but unstamped and chase the candidate twice.
		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(cancellationToken);

		if (released.Count == 0)
		{
			return 0;
		}

		// After the release committed, so the history reflects work that is actually
		// scheduled - the same ordering the resend paths use. Queueing an email is not a
		// step in the order lifecycle, so the status is written unchanged on both sides.
		await _orderHistoryService.RecordManyAsync(
			released.Select(r => r.EmailInvitationID).ToList(),
			OrderHistoryEventType.ApplicationFormFollowUpSent,
			null,
			OrderStatus.PendingCandidateInfo,
			cancellationToken);

		_logger.LogInformation(
			"Queued {ReleasedCount} application form follow-up reminder(s): {@Context}",
			released.Count,
			logContext);

		return released.Count;
	}

	// The scope rule applied to an identity-only projection, so a bulk action can filter
	// many rows without loading each whole invitation.
	private static bool IsOwnerWithinScope(EmailInvitationOwnerDTO owner, AtsAccessScope scope)
	{
		if (scope.AuthorizedClientIds is { } clientIds
			&& !(owner.ClientId.HasValue && clientIds.Contains(owner.ClientId.Value)))
		{
			return false;
		}

		return !scope.RequiredOwnerId.HasValue
			|| owner.RequestorId == scope.RequiredOwnerId.Value;
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
