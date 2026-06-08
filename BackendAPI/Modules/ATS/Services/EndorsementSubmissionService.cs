using DocumentFormat.OpenXml.Vml.Office;

namespace ATS.Services;

public class EndorsementSubmissionService : IEndorsementSubmissionService
{
	private readonly ILogger<EndorsementSubmissionService> _logger;
	private readonly IHashService _hashService;
	private readonly IConfiguration _configuration;
	private readonly ISecureToken _secureToken;
	private readonly IATSRepository _atsRepository;
	private readonly IObjectStorageService _objectStorageService;
	private readonly string _templateFileName;
	private readonly int _applicationFormExpiryInHours;
	
	public EndorsementSubmissionService(
		ILogger<EndorsementSubmissionService> logger,
		IATSRepository atsRepository,
		IConfiguration configuration,
		IHashService hashService,
		ISecureToken secureToken,
		IObjectStorageService objectStorageService)
	{
		_logger = logger;
		_hashService = hashService;
		_secureToken = secureToken;
		_configuration = configuration;
		_atsRepository = atsRepository;
		_objectStorageService = objectStorageService;
		_templateFileName = _configuration.GetSection("ATS").GetValue<string>("ATSBulkTemplatePath") ?? string.Empty;
		_applicationFormExpiryInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
	}

	public Task<string> GetBulkTemplateFileUrlAsync()
	{
		var bulkTemplateLink = _objectStorageService.GenerateDownloadUrlAsync(_templateFileName, TimeSpan.FromMinutes(15));

		return bulkTemplateLink;
	}

	public async Task<bool> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default)
	{
		_logger.LogInformation("Inserting Email invitaion request {EmailInvitationID}", emailInvitationRequestDTO.EmailInvitationID);

		var logContext = new
		{
			Action = "InsertData",
			Step = "StartInserting",
			Identity = $"{emailInvitationRequestDTO.FirstName} {emailInvitationRequestDTO.LastName}",
			Timestamp = DateTime.UtcNow
		};

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
		emailInvitationRequestDTO.HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours);

		EmailInvitationRequest emailInvitationRequest = emailInvitationRequestDTO.Adapt<EmailInvitationRequest>();
		
		try
		{
			await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

		}
		catch (Exception)
		{
			_logger.LogError("Failed Transaction: Failed to add transaction data record for {Tid}: {@Context}", transaction.Tid, logContext);
			throw new InternalServerException($"Failed to add transaction."); ;
		}


		return true;

	}

}
