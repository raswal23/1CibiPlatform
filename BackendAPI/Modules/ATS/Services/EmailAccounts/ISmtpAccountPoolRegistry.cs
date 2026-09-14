namespace ATS.Services.EmailAccounts;

/// <summary>
/// Owns one connection pool and one rate limiter per registered sender account, and holds the
/// breaker state the selector reads.
/// </summary>
/// <remarks>
/// This is the object that replaces the single process-wide <c>SmtpConnectionPool</c> and
/// <c>SmtpRateLimiter</c> in DI. Those two are no longer registered: there is no longer exactly
/// one of each, so asking the container for "the pool" has no answer. Every invariant they
/// documented still holds - they are still singletons for the lifetime of their account, a
/// failed send still does not discard its session, and concurrency still cannot outrun the
/// limiter - the scope of "the sender" simply narrowed from the process to one mailbox.
/// </remarks>
public interface ISmtpAccountPoolRegistry
{
	/// <summary>
	/// The highest-priority account that may carry the next message, or null when every
	/// registered account is capped, cooling down, unverified or disabled.
	/// </summary>
	/// <param name="excludedAccountIds">
	/// Accounts already tried for THIS message. A failover retries the same message on the
	/// next account, and without this it would be handed straight back the one that just
	/// refused it.
	/// </param>
	Task<AtsEmailAccountSnapshot?> GetNextSendableAccountAsync(
		IReadOnlyCollection<int> excludedAccountIds,
		CancellationToken cancellationToken);

	/// <summary>
	/// The pool and limiter for one account, built on first use from its stored credentials.
	/// </summary>
	/// <remarks>
	/// Throws when the account is gone or its password cannot be decrypted. A decrypt failure
	/// means the protection key changed, which is not recoverable by retrying.
	/// </remarks>
	Task<SmtpAccountContext> GetContextAsync(int accountId, CancellationToken cancellationToken);

	/// <summary>Records a success: resets the breaker and appends to the send log.</summary>
	Task ReportSuccessAsync(
		int accountId,
		int recipientCount,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records a failure against an account and returns whether it has now left rotation.
	/// </summary>
	/// <remarks>
	/// A failure whose <c>Scope</c> is <c>Message</c> is not the account's fault and does not
	/// reach the breaker at all - see the remark on <c>EmailFailureScope</c>.
	/// </remarks>
	Task<bool> ReportFailureAsync(
		int accountId,
		EmailDeliveryResult result,
		CancellationToken cancellationToken);

	/// <summary>
	/// True while this account holds an SMTP session for a send in flight. Edit and delete
	/// refuse during that window rather than pulling credentials out from under a live send.
	/// </summary>
	bool IsLeased(int accountId);

	/// <summary>
	/// Marks an account busy for one send. Dispose the handle to release it.
	/// </summary>
	/// <remarks>
	/// Always in a <c>using</c>. A lease that is taken and not released leaves the account
	/// permanently un-editable, and nothing times it out.
	/// </remarks>
	IDisposable Lease(int accountId);

	/// <summary>
	/// Drops any cached pool for an account whose credentials changed or which was deleted.
	/// </summary>
	/// <remarks>
	/// Must be called on every credential edit. A pool holds sessions authenticated with the
	/// OLD password; leaving it cached means the next send silently uses credentials the
	/// operator believes they replaced.
	/// </remarks>
	ValueTask InvalidateAsync(int accountId);
}
