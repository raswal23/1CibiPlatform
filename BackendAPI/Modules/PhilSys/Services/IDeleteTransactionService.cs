namespace PhilSys.Services;

public interface IDeleteTransactionService
{
	Task<bool> DeleteTransactionAsync(string HashToken);
}
