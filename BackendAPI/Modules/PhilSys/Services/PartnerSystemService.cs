namespace PhilSys.Services;

public class PartnerSystemService
{
	private readonly ILogger<PartnerSystemService> _logger;
	private readonly IPhilSysRepository _repository;
	private readonly IConfiguration _configuration;
	private readonly IHashService _hashService;
	private readonly ISecureToken _securetoken;
	private readonly double _livenessExpiryMinutes;
	private readonly string _livenessBaseUrl;
	public PartnerSystemService(
		ILogger<PartnerSystemService> logger, 
		IPhilSysRepository repository,
		IConfiguration configuration,
		IHashService hashService,
		ISecureToken securetoken)
	{
		_logger = logger;
		_repository = repository;
		_configuration = configuration;
		_hashService = hashService;
		_securetoken = securetoken;
		_livenessExpiryMinutes = int.Parse(_configuration["PhilSys:LivenessSessionExpiryInMinutes"] ?? "10");
		_livenessBaseUrl = _configuration["PhilSys:LivenessBaseUrl"] ?? "";
	}
	public async Task<PartnerSystemResponseDTO> PartnerSystemQueryAsync(string callback_url, string inquiry_type, IdentityData identity_data)
	{
		PhilSysTransaction transaction = new PhilSysTransaction { } ;

		var identifier = !string.IsNullOrWhiteSpace(identity_data.PCN)
							 ? identity_data.PCN
							 : $"{identity_data.FirstName} {identity_data.LastName}".Trim();

		var logContext = new
		{
			Action = "GettingLivenessLink",
			Step = "StartPostingPcnOrBasicInfo",
			Identity = identifier,
			CallbackUrl = callback_url,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Partner system query initiated: {@Context}", logContext); ;

		var token = _securetoken.GenerateSecureToken();

		if (token == null)
		{
			_logger.LogError("Failed Transaction: Failed to generate Token for identity: {@Context}", logContext);
			throw new Exception("Failed to generate Token.");
		}

		var HashToken = _hashService.Hash(token);

		if (HashToken == null)
		{
			_logger.LogError("Failed Transaction: Failed to hash Token for identity: {@Context}", logContext);
			throw new Exception("Failed to hash Token.");
		}

		if (inquiry_type.Equals("name_dob", StringComparison.OrdinalIgnoreCase))
		{
			transaction = new PhilSysTransaction
			{
				Tid = Guid.CreateVersion7(),
				InquiryType = "name_dob",
				FirstName = identity_data.FirstName,
				MiddleName = identity_data.MiddleName,
				LastName = identity_data.LastName,
				Suffix = identity_data.Suffix,
				BirthDate = identity_data.BirthDate,
				IsTransacted = false,
				HashToken = HashToken,
				WebHookUrl = callback_url,
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddMinutes(_livenessExpiryMinutes)
			};
		}

		else if (inquiry_type.Equals("pcn", StringComparison.OrdinalIgnoreCase))
		{
			transaction = new PhilSysTransaction
			{
				Tid = Guid.CreateVersion7(),
				InquiryType = "pcn",
				PCN = identity_data.PCN,
				IsTransacted = false,
				HashToken = HashToken,
				WebHookUrl = callback_url,
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddMinutes(_livenessExpiryMinutes)
			};
		}

		var livenessUrl = $"{_livenessBaseUrl}/philsys/idv/liveness/{transaction.HashToken}";

		try
		{
			var result = await _repository.AddTransactionDataAsync(transaction);
		}
		catch (Exception)
		{
			_logger.LogError("Failed Transaction: Failed to add transaction data record for {Tid}: {@Context}", transaction.Tid, logContext);
			throw new InternalServerException($"Failed to add transaction.");;
		}

		_logger.LogInformation("Succcessfully added the transaction data record for {Tid}: {@Context}", transaction.Tid, logContext);

		return new PartnerSystemResponseDTO(
			idv_session_id: transaction.Tid.ToString(),
			liveness_link: livenessUrl
		);
	}
}
