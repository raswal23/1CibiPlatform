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

	public async Task<bool> InsertEmailInvitationRequestAsync(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default)
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
		emailInvitationRequest.HashToken = HashToken;
		emailInvitationRequest.HashTokenCreatedAt = DateTime.UtcNow;
		emailInvitationRequest.OrderCreatedAt = DateTime.UtcNow;
		emailInvitationRequest.EmailSentStatus = EmailStatus.Pending;
		emailInvitationRequest.ApplicationFormStatus = ApplicationFormStatus.Pending;
		emailInvitationRequest.OrderStatus = OrderStatus.PendingCandidateInfo;
		emailInvitationRequest.RequestorId = _currentUser.UserId;
		emailInvitationRequest.ClientId = _currentUser.AtsClientId;
		emailInvitationRequest.Requestor = _currentUser.FullName;
		emailInvitationRequest.HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);

		try
		{
			await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to add Email Invitation Request. {@Context}",
				logContext);

			throw new InternalServerException(
				$"Failed to add transaction. {ex.InnerException?.Message ?? ex.Message}");
		}

		var applicationFormLink = $"{_applicationformBaseUrl}/{HashToken}";

		try
		{
			await SendApplicationFormToUserEmailAsync(
				emailInvitationRequestDTO.EmailAddress!,
				subjectName,
				applicationFormLink);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to send application form email. {@Context}",
				logContext);

			await TryUpdateEmailStatusToNotSentAsync(
				emailInvitationRequest.EmailInvitationID,
				logContext);

			throw new InternalServerException("Failed to send application form email.");
		}

		await _unitOfWork.BeginTransactionAsync(ct);

		try
		{
			await _atsRepository.UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(
				emailInvitationRequest.EmailInvitationID);

			await _orderHistoryService.RecordAsync(
				emailInvitationRequest.EmailInvitationID,
				OrderHistoryEventType.OrderCreated,
				null,
				OrderStatus.PendingCandidateInfo, ct);

			await _unitOfWork.SaveChangesAsync(ct);

			await _unitOfWork.CommitAsync(ct);
		}
		catch (Exception ex)
		{
			await _unitOfWork.RollbackAsync(ct);

			_logger.LogError(
				ex,
				"Email was sent successfully, but failed to update its status. {@Context}",
				logContext);

			throw new InternalServerException(
				"The email was sent successfully, but the system failed to update its status.");
		}

		return true;
	}
	private async Task TryUpdateEmailStatusToNotSentAsync(
	Guid emailInvitationId,
	object logContext)
	{
		try
		{
			await _atsRepository.UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(
				emailInvitationId);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to update email status to 'Not Sent'. {@Context}",
				logContext);
		}
	}

	public async Task<bool> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO, CancellationToken ct = default)
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
		bulkUploadFileDetails.Status = BulkFileStatus.Pending;
		bulkUploadFileDetails.DateCreated = DateTime.UtcNow;
		bulkUploadFileDetails.ClientId = _currentUser.AtsClientId;
		bulkUploadFileDetails.UploadedByUserId = _currentUser.UserId;
		bulkUploadFileDetails.FileKey = bulkFileKey;

		try
		{
			await _atsRepository.AddBulkUploadFileDetailsAsync(bulkUploadFileDetails);
			_logger.LogInformation("Successfully added the file info in the database and object storage - {FileID}: {@Context}", bulkUploadFileDetailsDTO.UploadedByUserId, logContext);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to insert data for Bulk File Information {FileID} : {@Context}", bulkUploadFileDetailsDTO.UploadedByUserId, logContext);
			await _objectStorageService.DeleteAsync(bulkFileKey, ct);
			throw new InternalServerException($"Failed insert data to the database. {ex.InnerException?.Message ?? ex.Message}");
		}

		return true;
	}

	public async Task<bool> SendApplicationFormToUserEmailAsync(string gmail, string name, string applicationFormLink)
	{
		var logContext = new
		{
			Action = "SendApplicationFormEmail",
			Step = "SendEmail",
			Email = gmail,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending notification for email: {@Context}", logContext);

		var otpBody = _emailService.SendAppplicationFormNotification(gmail, name, applicationFormLink);

		var isSent = await _emailService.SendATSEmailAsync(
			toEmail: gmail!,
			subject: "CIBI | Background Verification Information Request",
			body: otpBody
		);

		if (!isSent)
		{
			_logger.LogError("Failed to send Notification email to: {@Context}", logContext);
			throw new InternalServerException("Failed to send Notification email.");
		}

		return isSent;
	}

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetWithdrawnApplicationForm",
			Step = "FetchingWithdrawnApplicationForm",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching withdrawn application form with pagination: {@Context}", logContext);
		if (!_currentUser.IsAuthenticated
			|| _currentUser.UserId is not { } userId
			|| userId == Guid.Empty)
		{
			return CreateEmptyWithdrawnResult(paginationRequest);
		}

		IReadOnlyCollection<int>? clientIds;
		Guid? requiredRequestorId;
		if (_currentUser.IsPlatformSuperAdmin)
		{
			clientIds = null;
			requiredRequestorId = null;
		}
		else if (_currentUser.AtsRoleId is not { } roleId)
		{
			return CreateEmptyWithdrawnResult(paginationRequest);
		}
		else if (roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin)
		{
			var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
				[userId],
				cancellationToken);
			clientIds = assignments
				.Select(assignment => assignment.ClientId)
				.Distinct()
				.ToArray();
			requiredRequestorId = null;
		}
		else if (roleId is AtsRoleIds.User or AtsRoleIds.Uploader
			&& _currentUser.AtsClientId is { } clientId)
		{
			clientIds = [clientId];
			requiredRequestorId = userId;
		}
		else
		{
			return CreateEmptyWithdrawnResult(paginationRequest);
		}

		return await (string.IsNullOrEmpty(paginationRequest.SearchTerm)
			? _atsRepository.GetWithdrawnEmailInvitationRequestsAsync(
				paginationRequest,
				clientIds,
				requiredRequestorId,
				cancellationToken)
			: _atsRepository.SearchWithdrawnEmailInvitationRequestsAsync(
				paginationRequest,
				clientIds,
				requiredRequestorId,
				cancellationToken));
	}

	private static PaginatedResult<EmailInvitationRequestListDTO> CreateEmptyWithdrawnResult(
		PaginationRequest paginationRequest) =>
		new(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			0,
			Array.Empty<EmailInvitationRequestListDTO>());

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

		await _unitOfWork.BeginTransactionAsync(cancellationToken);

		try
		{
			await _atsRepository.ResendApplicationFormAsync(emailInvitationId, hashToken, newExpiration, cancellationToken);

			logContext = new
			{
				Action = "ResendApplicationForm",
				Step = "SendingEmail",
				EmailInvitationId = emailInvitationId,
				Timestamp = DateTime.UtcNow
			};

			var applicationFormLink = $"{_applicationformBaseUrl}/{hashToken}";
			var fullName = $"{invitation.FirstName} {invitation.LastName}";

			await SendApplicationFormToUserEmailAsync(
				invitation.EmailAddress!,
				fullName,
				applicationFormLink);

			await _orderHistoryService.RecordAsync(
				emailInvitationId,
				OrderHistoryEventType.ApplicationFormResent,
				invitation.OrderStatus,
				OrderStatus.PendingCandidateInfo,
				cancellationToken);

			await _unitOfWork.SaveChangesAsync(cancellationToken);

			await _unitOfWork.CommitAsync(cancellationToken);

			_logger.LogInformation("Successfully resent application form for invitation: {@Context}", logContext);
			return true;
		}
		catch (Exception ex)
		{
			await _unitOfWork.RollbackAsync(cancellationToken);

			_logger.LogError("Failed to resend application form: {@Context}, {Exception}", logContext, ex);
			throw new InternalServerException($"Failed to resend application form. {ex.InnerException?.Message ?? ex.Message}");
		}
	}
}
