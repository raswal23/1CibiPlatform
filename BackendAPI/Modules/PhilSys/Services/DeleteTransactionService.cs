namespace PhilSys.Services;
public class DeleteTransactionService : IDeleteTransactionService
{
	private readonly IPhilSysRepository _philSysRepository;
	private readonly ILogger<DeleteTransactionService> _logger;

	public DeleteTransactionService(IPhilSysRepository philSysRepository,
									ILogger<DeleteTransactionService> logger)
	{
		_philSysRepository = philSysRepository;
		_logger = logger;
	}

	public async Task<bool> DeleteTransactionAsync(string HashToken)
	{
		var logContext = new
		{
			Action = "DeletingPhilSysTransaction",
			Step = "StartDelete",
			HashToken,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Deleting PhilSys Transaction attempt for hashtoken: {@Context}", logContext);

		var existingTransaction = await _philSysRepository.GetTransactionDataByHashTokenAsync(HashToken);

		if (existingTransaction == null)
		{
			_logger.LogError("Deletion Failed: PhilSys Transaction with HashToken: {@Context} not found.", logContext);
			throw new NotFoundException("Transaction record not found.");
		}

		var deletedTransaction = await _philSysRepository.DeleteTransactionDataAsync(existingTransaction);

		if (deletedTransaction == false)
		{
			_logger.LogError("Failed to delete PhilSys Transaction record for HashToken: {HashToken}", HashToken);
			throw new Exception("Failed to delete the transaction record.");
		}

		_logger.LogInformation("Successfully Deleted the Transaction record for {HashToken}.", HashToken);

		return true;
	}
}
