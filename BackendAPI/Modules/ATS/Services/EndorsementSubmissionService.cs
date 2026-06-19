

namespace ATS.Services;

public class EndorsementSubmissionService : IEndorsementSubmissionService
{
	private readonly ILogger<EndorsementSubmissionService> _logger;
	private readonly IHashService _hashService;
	private readonly IEmailService _emailService;
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
		IEmailService emailService,
		ISecureToken secureToken,
		IHttpContextAccessor httpContextAccessor,
		IObjectStorageService objectStorageService)
	{
		_logger = logger;
		_hashService = hashService;
		_emailService = emailService;
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

	public Task<string> GetBulkTemplateFileUrlAsync()
	{
		var bulkTemplateLink = _objectStorageService.GenerateDownloadUrlAsync(_templateFileName, TimeSpan.FromMinutes(15));

		return bulkTemplateLink;
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

		emailInvitationRequestDTO.EmailInvitationID = Guid.CreateVersion7();
		emailInvitationRequestDTO.HashToken =HashToken;
		emailInvitationRequestDTO.HashTokenCreated = DateTime.UtcNow;
		emailInvitationRequestDTO.Status = "Pending";
		emailInvitationRequestDTO.HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);

		EmailInvitationRequest emailInvitationRequest = emailInvitationRequestDTO.Adapt<EmailInvitationRequest>();
		
		try
		{
			await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

		}
		catch (Exception ex)
		{
			_logger.LogError("Failed Transaction: Failed to add Email Invitation Request: {@Context}, {Exception}", logContext, ex);
			throw new InternalServerException($"Failed to add transaction. {ex.InnerException?.Message ?? ex.Message}"); ;
		}

		var applicationFormLink = $"{_applicationformBaseUrl}?hashToken={HashToken}";

		try
		{
			await SendApplicationFormToUserEmailAsync(
				emailInvitationRequestDTO.EmailAddress!,
				subjectName,
				applicationFormLink);

			await _atsRepository.UpdateEmailInvitationRequestStatusAsync(emailInvitationRequest.EmailInvitationID, "Sent");
		}
		catch (Exception ex)
		{
			_logger.LogError("Failed to send email: {@Context}, {Exception}", logContext, ex);

			await _atsRepository.UpdateEmailInvitationRequestStatusAsync(emailInvitationRequest.EmailInvitationID, "Error");

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
			Identity = bulkUploadFileDetailsDTO.FileID,
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

		bulkUploadFileDetailsDTO.FileID = Guid.CreateVersion7();
		bulkUploadFileDetailsDTO.Status = "Pending";
		bulkUploadFileDetailsDTO.DateCreated = DateTime.UtcNow;

		BulkUploadFileDetails bulkUploadFileDetails = bulkUploadFileDetailsDTO.Adapt<BulkUploadFileDetails>();
		bulkUploadFileDetails.FileKey = bulkFileKey;

		try
		{
			await _atsRepository.AddBulkUploadFileDetailsAsync(bulkUploadFileDetails);
			_logger.LogInformation("Successfully added the file info in the database and object storage - {FileID}: {@Context}", bulkUploadFileDetailsDTO.FileID, logContext);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to insert data for Bulk File Information {FileID} : {@Context}", bulkUploadFileDetailsDTO.FileID, logContext);
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

		var isSent = await _emailService.SendEmailAsync(
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

}
