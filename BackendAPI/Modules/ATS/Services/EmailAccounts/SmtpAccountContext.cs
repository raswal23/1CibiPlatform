namespace ATS.Services.EmailAccounts;

/// <summary>
/// One account's sending machinery: its connection pool, its rate limiter, and the identity to
/// put in the From header.
/// </summary>
/// <remarks>
/// Held together because they are only ever correct as a set. A pool authenticated as account A
/// paced by account B's limiter would let A send at twice its agreed rate; a message built with
/// A's display name but sent down B's connection arrives with a From that the receiving server
/// may reject outright as a spoof.
/// </remarks>
public sealed class SmtpAccountContext : IAsyncDisposable
{
	public SmtpAccountContext(
		int atsEmailAccountId,
		string displayName,
		string emailAddress,
		SmtpConnectionPool pool,
		SmtpRateLimiter rateLimiter)
	{
		AtsEmailAccountId = atsEmailAccountId;
		DisplayName = displayName;
		EmailAddress = emailAddress;
		Pool = pool;
		RateLimiter = rateLimiter;
	}

	public int AtsEmailAccountId { get; }

	public string DisplayName { get; }

	public string EmailAddress { get; }

	public SmtpConnectionPool Pool { get; }

	public SmtpRateLimiter RateLimiter { get; }

	public async ValueTask DisposeAsync()
	{
		await Pool.DisposeAsync();

		RateLimiter.Dispose();
	}
}
