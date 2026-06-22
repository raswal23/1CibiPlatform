namespace PhilSys.Services;

public class LivenessSessionService
{
	private readonly IPhilSysRepository _philSysRepository;
	private readonly IHashService _hashService;
	private IConfiguration _configuration;
	private readonly ILogger<LivenessSessionService> _logger;
	private readonly string? _applicationFormPath;

	public LivenessSessionService(IPhilSysRepository philSysRepository,
								  IHashService hashService,
								  IConfiguration configuration,
								  ILogger<LivenessSessionService> logger)
	{
		_philSysRepository = philSysRepository;
		_hashService = hashService;
		_configuration = configuration;
		_applicationFormPath = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
		_logger = logger;
	}
	public async Task<TransactionStatusResponseDTO> IsLivenessUsedAsync(string HashToken)
	{
		var logContext = new
		{
			Action = "CheckingPhilSysTransactionStatus",
			Step = "StartChecking",
			HashToken,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Checking Transaction Status for HashToken: {@Context}", logContext);

		var status = await _philSysRepository.GetLivenessSessionStatusAsync(HashToken);

		if (status == null)
		{
			_logger.LogError("Checking PhilSys Transaction Failed: There is no transaction for HashToken: {@Context}", logContext);
			throw new NotFoundException($"There is no such transaction created for your liveness check.");
		}

		var hashTokenChecker = await _philSysRepository.GetTransactionDataByHashTokenAsync(HashToken);

		if (hashTokenChecker == null)
		{
			_logger.LogError("Checking PhilSys Transaction Failed: There is no transaction for HashToken: {@Context}", logContext);
			throw new NotFoundException($"There is no such transaction created for your liveness check.");
		}

		var isTokenValid = _hashService.Verify(HashToken, hashTokenChecker.HashToken!);

		if (!isTokenValid)
		{
			_logger.LogError("Checking PhilSys Transaction Failed:  Invalid Token Provided: {@Context}", logContext);
			throw new InternalServerException("Unable to Proceed. Invalid Token provided.");
		}

		_logger.LogInformation("Successfully received the transaction record for HashToken: {@Context}", logContext);

		var transactionStatusResponseDTO = status.Adapt<TransactionStatusResponseDTO>();
		transactionStatusResponseDTO.ATSApplicationFormPath = _applicationFormPath;
		return transactionStatusResponseDTO;
	}
}
