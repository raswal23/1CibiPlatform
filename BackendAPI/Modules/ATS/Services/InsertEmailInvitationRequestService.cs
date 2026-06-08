namespace ATS.Services;

public class InsertEmailInvitationRequestService : IInsertEmailInvitationRequestService
{
	private readonly ILogger<InsertEmailInvitationRequestService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IObjectStorageService _objectStorageService;
	private readonly ISecureToken _securetoken;
	private readonly IConfiguration _configuration;
	private readonly IHashService _hashService;
	private readonly IUnitOfWork _unitOfWork;
	private readonly double _emailInvitationRequestExpiryMinutes;

	public InsertEmailInvitationRequestService(
		ILogger<InsertEmailInvitationRequestService> logger,
		IATSRepository atsRepository, 
		IObjectStorageService objectStorageService, 
		IConfiguration configuration,
		IHashService hashService,
		ISecureToken secureToken,
		IUnitOfWork unitOfWork)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_objectStorageService = objectStorageService;
		_configuration = configuration; 
		_securetoken = secureToken;
		_hashService = hashService;
		_unitOfWork = unitOfWork;
		
		var expiryRaw = _configuration["ATS:EmailRequestInvitationExpiryInMinutes"];
		double parsedExpiry;
		if (!double.TryParse(expiryRaw, out parsedExpiry))
		{
			_logger.LogWarning("Invalid or missing configuration 'ATS:EmailRequestInvitationExpiryInMinutes' ('{Value}'), falling back to default 10 minutes.", expiryRaw);
			parsedExpiry = 10;
		}
		_emailInvitationRequestExpiryMinutes = parsedExpiry;

	}

	public async Task<Guid> InsertEmailInvitationRequest(EmailInvitationRequestDTO emailInvitationRequestDTO, CancellationToken ct = default)
	{
		_logger.LogInformation("Inserting Email invitaion request {EmailInvitationID}", emailInvitationRequestDTO.EmailInvitationID);

		var entity = emailInvitationRequestDTO.Adapt<EmailInvitationRequest>();
		
		var token = _securetoken.GenerateSecureToken();

		entity.EmailInvitationID = Guid.NewGuid();
		entity.HashToken = _hashService.Hash(token);
		entity.HashTokenCreated = DateTime.UtcNow;
		entity.HashTokenExpiration = entity.HashTokenCreated.Value.AddMinutes(_emailInvitationRequestExpiryMinutes);

		var logContext = new
		{
			Action = "InsertData",
			Step = "StartInserting",
			Identity = entity.EmailInvitationID,
			Timestamp = DateTime.UtcNow
		};

		if (string.IsNullOrEmpty(token))
		{
			_logger.LogError("Failed Transaction: Failed to generate Token for identity: {@Context}", logContext);
			throw new Exception("Failed to generate Token.");
		}

		if (string.IsNullOrEmpty(entity.HashToken))
		{
			_logger.LogError("Failed Transaction: Failed to hash Token for identity: {@Context}", logContext);
			throw new Exception("Failed to hash Token.");
		}

			await _atsRepository.AddEmailInvitationRequestAsync(entity);

			return entity.EmailInvitationID;

	}

}

