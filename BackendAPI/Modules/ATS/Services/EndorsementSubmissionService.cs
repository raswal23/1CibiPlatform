namespace ATS.Services;

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
		IObjectStorageService objectStorageService)
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
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
		_templateFileName = _configuration.GetSection("ATS").GetValue<string>("ATSBulkTemplatePath") ?? string.Empty;
		_applicationFormExpiryInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
		_folderName = _configuration["ATS:ATSUploadFolderName"] ?? "";
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
		emailInvitationRequest.HashToken =HashToken;
		emailInvitationRequest.HashTokenCreatedAt = DateTime.UtcNow;
		emailInvitationRequest.EmailSentStatus = "Pending";
		emailInvitationRequest.ApplicationFormStatus = "Pending";
		emailInvitationRequest.OrderStatus = "Pending Candidate Info";
		emailInvitationRequest.HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);
		
		try
		{
			await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

		}
		catch (Exception ex)
		{
			_logger.LogError("Failed Transaction: Failed to add Email Invitation Request: {@Context}, {Exception}", logContext, ex);
			throw new InternalServerException($"Failed to add transaction. {ex.InnerException?.Message ?? ex.Message}"); ;
		}

		var applicationFormLink = $"{_applicationformBaseUrl}/{HashToken}";

		try
		{
			await SendApplicationFormToUserEmailAsync(
				emailInvitationRequestDTO.EmailAddress!,
				subjectName,
				applicationFormLink);

			await _atsRepository.UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(emailInvitationRequest.EmailInvitationID);
		}
		catch (Exception ex)
		{
			_logger.LogError("Failed to send email: {@Context}, {Exception}", logContext, ex);

			await _atsRepository.UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(emailInvitationRequest.EmailInvitationID);

			throw new InternalServerException("Failed to send email.");
		}

		return true;
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
		bulkUploadFileDetails.Status = "Pending";
		bulkUploadFileDetails.DateCreated = DateTime.UtcNow;
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

		return true;
	}

	public Task<PaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetWithdrawnApplicationForm",
			Step = "FetchingWithdrawnApplicationForm",
			Pagination = paginationRequest,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching withdrawn application form with pagination: {@Context}", logContext);

		return string.IsNullOrEmpty(paginationRequest.SearchTerm) ? 
			_atsRepository.GetWithdrawnEmailInvitationRequestsAsync(paginationRequest, cancellationToken) :
			_atsRepository.SearchWithdrawnEmailInvitationRequestsAsync(paginationRequest, cancellationToken);
	}

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

			_logger.LogInformation("Successfully resent application form for invitation: {@Context}", logContext);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError("Failed to resend application form: {@Context}, {Exception}", logContext, ex);
			throw new InternalServerException($"Failed to resend application form. {ex.InnerException?.Message ?? ex.Message}");
		}
	}
}
