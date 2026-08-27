namespace OMS.Data.Connection;

public sealed class OMSSqlConnectionFactory(string connectionString) : IOMSSqlConnectionFactory
{
	public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
	{
		// Validated lazily instead of at registration so hosts without an OMS
		// secret (e.g. the Testing environment) can still boot.
		if (string.IsNullOrWhiteSpace(connectionString) ||
			connectionString.StartsWith("${", StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"The OMS_Connection connection string is not configured.");
		}

		var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		return connection;
	}
}
