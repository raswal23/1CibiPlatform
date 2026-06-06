namespace PhilSys.Services;

public class GetLivenessKeyService
{
	private readonly ILogger<GetLivenessKeyService> _logger;
	private readonly IConfiguration _configuration;
	private string _livenessKey;

	public GetLivenessKeyService(ILogger<GetLivenessKeyService> logger, IConfiguration configuration)
	{
		_logger = logger;
		_configuration = configuration;
		_livenessKey = _configuration["PhilSys:LivenessSDKPublicKey"] ?? "";
	}
	public Task<string> GetLivenessKey()
	{
		var logContext = new
		{
			Action = "RetrievingLivenessKey",
			Step = "StartFetching",
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Retrieving Liveness Key: {@Context}", logContext);
		if (string.IsNullOrEmpty(_livenessKey))
		{
			_logger.LogError("Liveness Key is not configured: {@Context}", logContext);
			return Task.FromResult(string.Empty);
		}

		_logger.LogInformation("Liveness Key retrieved successfully: {@Context}", logContext);
		return Task.FromResult(_livenessKey);
	}
}
