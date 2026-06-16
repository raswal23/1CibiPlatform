namespace ATS.Shared.Contracts;

public interface IATSQueries
{
	Task<string?> IsHashTokenValidAsync(
		string hashToken, 
		CancellationToken cancellationToken);
}
