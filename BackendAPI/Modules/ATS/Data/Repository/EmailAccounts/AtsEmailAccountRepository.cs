namespace ATS.Data.Repository.EmailAccounts;

// Uncached on purpose - see the remark on IAtsEmailAccountRepository. A cached view of which
// account is healthy is worse than no view at all.
public sealed class AtsEmailAccountRepository : IAtsEmailAccountRepository
{
	private readonly ATSDBContext _dbContext;
	private readonly AtsEmailDeliveryOptions _options;

	public AtsEmailAccountRepository(
		ATSDBContext dbContext,
		IOptions<AtsEmailDeliveryOptions> options)
	{
		_dbContext = dbContext;
		_options = options.Value;
	}

	private TimeSpan QuotaWindow =>
		TimeSpan.FromHours(Math.Max(1, _options.QuotaWindowHours));

	public Task<List<AtsEmailAccountSnapshot>> GetSnapshotsAsync(
		CancellationToken cancellationToken) =>
		BuildSnapshotQuery(query => query.OrderBy(account => account.Priority))
			.ToListAsync(cancellationToken);

	public Task<AtsEmailAccountSnapshot?> GetSnapshotAsync(
		int accountId,
		CancellationToken cancellationToken) =>
		BuildSnapshotQuery(query =>
				query.Where(account => account.AtsEmailAccountId == accountId))
			.FirstOrDefaultAsync(cancellationToken);

	/// <summary>
	/// Accounts with their rolling-window consumption, as ONE query.
	/// </summary>
	/// <remarks>
	/// The consumption is a correlated subquery rather than a second round trip per account.
	/// The selector calls this on the send path, so N+1 here would put N database calls on the
	/// critical path of every message - and the figure it produces decides where that message
	/// goes, so it cannot be deferred or cached either.
	///
	/// It sums RECIPIENTS, not rows. Google counts recipients against the daily cap, and while
	/// an invitation is 1:1 today, a future CC would consume more quota than a row count
	/// reports - which would show headroom that does not exist.
	/// </remarks>
	/// <param name="shape">
	/// Filtering and ordering applied to the ENTITY, before the projection. It has to happen on
	/// this side: ordering or filtering the projected records makes EF try to translate a
	/// comparison over the whole AtsEmailAccountSnapshot constructor, which it cannot do, and
	/// the query throws at runtime rather than failing to compile.
	/// </param>
	private IQueryable<AtsEmailAccountSnapshot> BuildSnapshotQuery(
		Func<IQueryable<AtsEmailAccount>, IQueryable<AtsEmailAccount>>? shape = null)
	{
		// Evaluated once here rather than inside the projection: DateTime.UtcNow per row would
		// give each account a slightly different window, and EF cannot translate it anyway.
		var windowStart = DateTime.UtcNow - QuotaWindow;

		var accounts = _dbContext.EmailAccounts.AsNoTracking();

		if (shape is not null)
		{
			accounts = shape(accounts);
		}

		return accounts
			.Select(account => new AtsEmailAccountSnapshot(
				account.AtsEmailAccountId,
				account.DisplayName,
				account.EmailAddress,
				account.SmtpHost,
				account.SmtpPort,
				account.Priority,
				account.IsActive,
				account.DailySendLimit,
				account.VerificationStatus,
				account.VerifiedAt,
				account.ConsecutiveFailureCount,
				account.CoolingDownUntil,
				account.LastFailureReason,
				account.LastSentAt,
				_dbContext.EmailSendLog
					.Where(log => log.AtsEmailAccountId == account.AtsEmailAccountId
						&& log.SentAt >= windowStart)
					.Sum(log => (int?)log.RecipientCount) ?? 0));
	}

	public Task<AtsEmailAccount?> GetAccountAsync(
		int accountId,
		CancellationToken cancellationToken) =>
		_dbContext.EmailAccounts
			.FirstOrDefaultAsync(
				account => account.AtsEmailAccountId == accountId,
				cancellationToken);

	public Task<AtsEmailAccount?> GetAccountByEmailAsync(
		string emailAddress,
		CancellationToken cancellationToken) =>
		_dbContext.EmailAccounts
			.FirstOrDefaultAsync(
				account => account.EmailAddress.ToLower() == emailAddress.ToLower(),
				cancellationToken);

	public Task<bool> EmailAddressExistsAsync(
		string emailAddress,
		int? excludingAccountId,
		CancellationToken cancellationToken) =>
		_dbContext.EmailAccounts
			.AsNoTracking()
			.AnyAsync(
				account => account.EmailAddress.ToLower() == emailAddress.ToLower()
					&& (excludingAccountId == null
						|| account.AtsEmailAccountId != excludingAccountId),
				cancellationToken);

	public Task<bool> PriorityExistsAsync(
		int priority,
		int? excludingAccountId,
		CancellationToken cancellationToken) =>
		_dbContext.EmailAccounts
			.AsNoTracking()
			.AnyAsync(
				account => account.Priority == priority
					&& (excludingAccountId == null
						|| account.AtsEmailAccountId != excludingAccountId),
				cancellationToken);

	public async Task<int> GetNextAvailablePriorityAsync(CancellationToken cancellationToken)
	{
		var highest = await _dbContext.EmailAccounts
			.AsNoTracking()
			.OrderByDescending(account => account.Priority)
			.Select(account => (int?)account.Priority)
			.FirstOrDefaultAsync(cancellationToken);

		return (highest ?? 0) + 1;
	}

	public async Task AddAsync(AtsEmailAccount account, CancellationToken cancellationToken)
	{
		_dbContext.EmailAccounts.Add(account);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task UpdateAsync(AtsEmailAccount account, CancellationToken cancellationToken)
	{
		account.UpdatedAt = DateTime.UtcNow;

		_dbContext.EmailAccounts.Update(account);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteAsync(AtsEmailAccount account, CancellationToken cancellationToken)
	{
		_dbContext.EmailAccounts.Remove(account);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task RecordSuccessfulSendAsync(
		int accountId,
		int recipientCount,
		DateTime sentAtUtc,
		CancellationToken cancellationToken)
	{
		_dbContext.EmailSendLog.Add(new AtsEmailSendLog
		{
			AtsEmailAccountId = accountId,
			RecipientCount = recipientCount,
			SentAt = sentAtUtc
		});

		// The log row first: it is what the quota is counted from, so if the process dies
		// between the two writes the account looks slightly MORE consumed than it is. The
		// opposite ordering would understate consumption and could take the account past the
		// provider's cap, which costs a 24-hour lockout rather than one wasted message.
		await _dbContext.SaveChangesAsync(cancellationToken);

		// ExecuteUpdate rather than load-modify-save: sends run concurrently, and reading the
		// row to zero a counter would let two threads write each other's stale values. This is
		// also the reset half of the breaker - a success clears the consecutive count, so
		// three failures separated by a success never add up to a trip.
		//
		// CoolingDownUntil is deliberately NOT cleared here. A send can succeed on a
		// connection opened before a throttle was recorded, and letting that success cancel
		// the back-off would put the account straight back into a rate limit it is already in.
		// Cooling down expires on its own clock.
		await _dbContext.EmailAccounts
			.Where(account => account.AtsEmailAccountId == accountId)
			.ExecuteUpdateAsync(
				setters => setters
					.SetProperty(account => account.ConsecutiveFailureCount, 0)
					.SetProperty(account => account.LastFailureReason, (string?)null)
					.SetProperty(account => account.LastSentAt, sentAtUtc)
					.SetProperty(account => account.UpdatedAt, DateTime.UtcNow),
				cancellationToken);
	}

	public async Task RecordHealthAsync(
		int accountId,
		int consecutiveFailureCount,
		DateTime? coolingDownUntil,
		string? lastFailureReason,
		string? verificationStatus,
		CancellationToken cancellationToken)
	{
		var account = await _dbContext.EmailAccounts
			.FirstOrDefaultAsync(
				candidate => candidate.AtsEmailAccountId == accountId,
				cancellationToken);

		if (account is null)
		{
			// Deleted while a send was in flight. Nothing to write, and failing here would
			// turn a routine race into a send error.
			return;
		}

		account.ConsecutiveFailureCount = consecutiveFailureCount;
		account.CoolingDownUntil = coolingDownUntil;
		account.LastFailureReason = lastFailureReason;
		account.UpdatedAt = DateTime.UtcNow;

		// Only ever set, never cleared from here: clearing the status is verification's job,
		// and a health write must not be able to put an unproven credential back in rotation.
		if (verificationStatus is not null)
		{
			account.VerificationStatus = verificationStatus;
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<int> GetConsumedInWindowAsync(
		int accountId,
		DateTime windowStartUtc,
		CancellationToken cancellationToken) =>
		await _dbContext.EmailSendLog
			.AsNoTracking()
			.Where(log => log.AtsEmailAccountId == accountId && log.SentAt >= windowStartUtc)
			.SumAsync(log => (int?)log.RecipientCount, cancellationToken) ?? 0;

	public Task<int> DeleteSendLogsOlderThanAsync(
		DateTime cutoffUtc,
		CancellationToken cancellationToken) =>
		_dbContext.EmailSendLog
			.Where(log => log.SentAt < cutoffUtc)
			.ExecuteDeleteAsync(cancellationToken);

	#region One-time codes
	public async Task AddOtpAsync(AtsEmailAccountOtp otp, CancellationToken cancellationToken)
	{
		_dbContext.EmailAccountOtp.Add(otp);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public Task<AtsEmailAccountOtp?> GetActiveOtpAsync(
		int accountId,
		string purpose,
		DateTime asOfUtc,
		CancellationToken cancellationToken) =>
		_dbContext.EmailAccountOtp
			.Where(otp => otp.AtsEmailAccountId == accountId
				&& otp.Purpose == purpose
				&& !otp.IsUsed
				&& otp.ExpiresAt > asOfUtc)
			.OrderByDescending(otp => otp.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task UpdateOtpAsync(
		AtsEmailAccountOtp otp,
		CancellationToken cancellationToken)
	{
		_dbContext.EmailAccountOtp.Update(otp);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public Task InvalidateOtpsAsync(
		int accountId,
		string purpose,
		CancellationToken cancellationToken) =>
		_dbContext.EmailAccountOtp
			.Where(otp => otp.AtsEmailAccountId == accountId
				&& otp.Purpose == purpose
				&& !otp.IsUsed)
			.ExecuteUpdateAsync(
				setters => setters.SetProperty(otp => otp.IsUsed, true),
				cancellationToken);
	#endregion
}
