namespace ATS.Shared.Contracts;

public interface IATSQueries
{
	Task<bool> IsHashTokenValidAsync(
		string hashToken, 
		CancellationToken cancellationToken);
}
