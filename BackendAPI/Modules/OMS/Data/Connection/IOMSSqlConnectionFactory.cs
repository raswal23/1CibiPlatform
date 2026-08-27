namespace OMS.Data.Connection;

public interface IOMSSqlConnectionFactory
{
	Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
