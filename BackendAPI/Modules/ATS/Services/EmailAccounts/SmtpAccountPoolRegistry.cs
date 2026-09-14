namespace ATS.Services.EmailAccounts;

/// <summary>
/// The singleton that replaces the one-per-process connection pool and rate limiter.
/// </summary>
/// <remarks>
/// Singleton for the same reason the pool was: the resources it holds - authenticated SMTP
/// sessions and a send-rate budget - belong to a sending account, not to a request. Rebuilding
/// them per scope would reopen and re-authenticate a session per operation, which is the exact
/// behaviour that got this sender throttled at 14 messages.
///
/// Because it is a singleton it CANNOT inject <c>ATSDBContext</c>, which is scoped and not
/// thread-safe. It takes <c>IServiceScopeFactory</c> and opens a scope per database touch - the
/// same pattern <c>EmailNotificationProcessorService</c> already uses per send attempt, and for
/// the same reason.
///
/// Health lives in two places on purpose. The breaker counter is held in memory because the
/// selector consults it per message and a round trip per send would be wasteful; it is also
/// written through to the row, because a process restart must not resurrect an account the
/// provider is still throttling, and the management table has to show health without reading
/// process memory. The database is the authority on restart, memory is the authority while
/// running.
/// </remarks>
public sealed class SmtpAccountPoolRegistry : ISmtpAccountPoolRegistry, IAsyncDisposable
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ISecretProtector _secretProtector;
	private readonly IOptions<AtsEmailDeliveryOptions> _options;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ILogger<SmtpAccountPoolRegistry> _logger;

	// One entry per account that has actually sent something. Built on demand rather than at
	// start-up: opening a session costs a login, and an account registered but never selected
	// should not spend one.
	private readonly ConcurrentDictionary<int, SmtpAccountContext> _contexts = new();

	// Serialises context CREATION per account. Without it, two concurrent sends to the same
	// new account would each build a pool, and the loser's would be dropped with its sessions
	// still open - a login spent for nothing, which is the resource this whole design protects.
	private readonly ConcurrentDictionary<int, SemaphoreSlim> _contextLocks = new();

	// Live sends per account. Non-zero means edit and delete must refuse: swapping credentials
	// under an in-flight send either fails it or, worse, sends it from the wrong mailbox.
	private readonly ConcurrentDictionary<int, int> _leaseCounts = new();

	private bool _disposed;

	public SmtpAccountPoolRegistry(
		IServiceScopeFactory scopeFactory,
		ISecretProtector secretProtector,
		IOptions<AtsEmailDeliveryOptions> options,
		ILoggerFactory loggerFactory,
		ILogger<SmtpAccountPoolRegistry> logger)
	{
		_scopeFactory = scopeFactory;
		_secretProtector = secretProtector;
		_options = options;
		_loggerFactory = loggerFactory;
		_logger = logger;
	}

	public async Task<AtsEmailAccountSnapshot?> GetNextSendableAccountAsync(
		IReadOnlyCollection<int> excludedAccountIds,
		CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();

		var repository = scope.ServiceProvider
			.GetRequiredService<IAtsEmailAccountRepository>();

		var snapshots = await repository.GetSnapshotsAsync(cancellationToken);

		// Evaluated once so every candidate is judged against the same instant. Comparing each
		// account to its own DateTime.UtcNow would let a cooldown that expires mid-loop make
		// the choice depend on evaluation order.
		var now = DateTime.UtcNow;

		// Already ordered by priority in the query. Lower wins, and the order is unambiguous
		// because Priority is uniquely indexed - a tie would make failover depend on row order.
		return snapshots
			.Where(snapshot => !excludedAccountIds.Contains(snapshot.AtsEmailAccountId))
			.FirstOrDefault(snapshot => snapshot.IsSendable(now));
	}

	public async Task<SmtpAccountContext> GetContextAsync(
		int accountId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_contexts.TryGetValue(accountId, out var existing))
		{
			return existing;
		}

		var gate = _contextLocks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));

		await gate.WaitAsync(cancellationToken);

		try
		{
			// Re-checked inside the lock: the thread that was waiting here is usually waiting
			// precisely because another one was building the context it wants.
			if (_contexts.TryGetValue(accountId, out existing))
			{
				return existing;
			}

			var context = await BuildContextAsync(accountId, cancellationToken);

			_contexts[accountId] = context;

			return context;
		}
		finally
		{
			gate.Release();
		}
	}

	private async Task<SmtpAccountContext> BuildContextAsync(
		int accountId,
		CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();

		var repository = scope.ServiceProvider
			.GetRequiredService<IAtsEmailAccountRepository>();

		var account = await repository.GetAccountAsync(accountId, cancellationToken)
			?? throw new InvalidOperationException(
				$"Sender account {accountId} no longer exists.");

		string appPassword;

		try
		{
			appPassword = _secretProtector.Unprotect(
				account.EncryptedPassword,
				AtsEmailAccountSecrets.PasswordContext(account.EmailAddress));
		}
		catch (System.Security.Cryptography.CryptographicException exception)
		{
			// The protection key changed, or the row was tampered with. Neither is fixed by
			// retrying, and the message must not include the ciphertext.
			throw new InvalidOperationException(
				$"The stored password for sender account {accountId} could not be read. "
				+ "It must be re-entered and re-verified.",
				exception);
		}

		// Its own limiter, not a shared one. Two accounts each get the configured rate, which
		// is the point: the provider budgets per mailbox, so sharing one budget across two
		// mailboxes would halve the throughput this feature exists to add.
		var rateLimiter = new SmtpRateLimiter(
			_options,
			_loggerFactory.CreateLogger<SmtpRateLimiter>());

		var pool = new SmtpConnectionPool(
			new SmtpAccountCredentials(
				account.AtsEmailAccountId,
				account.DisplayName,
				account.EmailAddress,
				appPassword,
				account.SmtpHost,
				account.SmtpPort),
			_options,
			rateLimiter,
			_loggerFactory.CreateLogger<SmtpConnectionPool>());

		_logger.LogInformation(
			"Built an SMTP context for sender account {AccountId} ({Email}).",
			account.AtsEmailAccountId,
			account.EmailAddress);

		return new SmtpAccountContext(
			account.AtsEmailAccountId,
			account.DisplayName,
			account.EmailAddress,
			pool,
			rateLimiter);
	}

	public async Task ReportSuccessAsync(
		int accountId,
		int recipientCount,
		CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();

		var repository = scope.ServiceProvider
			.GetRequiredService<IAtsEmailAccountRepository>();

		// Appends the log row AND resets the breaker in one call, so consumption and health
		// can never disagree about whether this send happened.
		await repository.RecordSuccessfulSendAsync(
			accountId,
			recipientCount,
			DateTime.UtcNow,
			cancellationToken);
	}

	public async Task<bool> ReportFailureAsync(
		int accountId,
		EmailDeliveryResult result,
		CancellationToken cancellationToken)
	{
		// The rule this whole design turns on: a failure about the RECIPIENT is not evidence
		// about the ACCOUNT. A "550 no such mailbox" counted here would let one bulk upload of
		// typo'd addresses retire every registered sender in minutes, leaving the queue with
		// nowhere to go and nothing actually wrong.
		if (!result.IsAccountFault)
		{
			return false;
		}

		var now = DateTime.UtcNow;
		var reason = Describe(result);

		using var scope = _scopeFactory.CreateScope();

		var repository = scope.ServiceProvider
			.GetRequiredService<IAtsEmailAccountRepository>();

		// A throttle needs no counting. The provider has already said this account is sending
		// too fast, and a second opinion costs another message against a closed door.
		if (result.Outcome == EmailDeliveryOutcome.Throttled)
		{
			var until = now.AddSeconds(_options.Value.ThrottleBackoffSeconds);

			// Also parked in memory, so the in-flight pass stops using this account
			// immediately rather than on its next database read.
			if (_contexts.TryGetValue(accountId, out var throttledContext))
			{
				throttledContext.RateLimiter.ReportThrottled(
					TimeSpan.FromSeconds(_options.Value.ThrottleBackoffSeconds));
			}

			await repository.RecordHealthAsync(
				accountId,
				consecutiveFailureCount: 0,
				coolingDownUntil: until,
				lastFailureReason: reason,
				verificationStatus: null,
				cancellationToken);

			_logger.LogWarning(
				"Sender account {AccountId} was throttled ({StatusCode}). Cooling down until {Until:O}.",
				accountId,
				result.StatusCode,
				until);

			return true;
		}

		// A permanent, account-scoped failure is a rejected credential or a disowned mailbox.
		// Waiting fixes neither, so the account leaves rotation until a human re-verifies it
		// rather than cooling down and coming back to fail identically.
		if (result.Outcome == EmailDeliveryOutcome.Permanent)
		{
			await repository.RecordHealthAsync(
				accountId,
				consecutiveFailureCount: 0,
				coolingDownUntil: null,
				lastFailureReason: reason,
				verificationStatus: AtsEmailAccountStatus.NeedsReverification,
				cancellationToken);

			await InvalidateAsync(accountId);

			_logger.LogError(
				"Sender account {AccountId} was rejected by the provider and now needs re-verification: {Reason}",
				accountId,
				reason);

			return true;
		}

		// Transient: counted, because one dropped socket is noise and three in a row is a
		// pattern. Read-then-write rather than an atomic increment is acceptable here - two
		// concurrent failures losing one count delays a trip by a single message, while the
		// row read is needed anyway to decide whether the threshold was crossed.
		var snapshot = await repository.GetSnapshotAsync(accountId, cancellationToken);

		if (snapshot is null)
		{
			return false;
		}

		var failureCount = snapshot.ConsecutiveFailureCount + 1;
		var threshold = Math.Max(1, _options.Value.ConsecutiveFailureThreshold);
		var tripped = failureCount >= threshold;

		var coolingDownUntil = tripped
			? now.AddSeconds(_options.Value.TransientFailureCooldownSeconds)
			: (DateTime?)null;

		await repository.RecordHealthAsync(
			accountId,
			// Reset on trip: the count has done its job, and leaving it at the threshold would
			// re-trip the account on its first failure after the cooldown rather than giving
			// it a fresh three.
			consecutiveFailureCount: tripped ? 0 : failureCount,
			coolingDownUntil: coolingDownUntil,
			lastFailureReason: reason,
			verificationStatus: null,
			cancellationToken);

		if (tripped)
		{
			_logger.LogWarning(
				"Sender account {AccountId} hit {Threshold} consecutive failures. Cooling down until {Until:O}. Last reason: {Reason}",
				accountId,
				threshold,
				coolingDownUntil,
				reason);
		}

		return tripped;
	}

	public bool IsLeased(int accountId) =>
		_leaseCounts.TryGetValue(accountId, out var count) && count > 0;

	/// <summary>
	/// Marks an account busy for the duration of one send. Dispose the returned scope to
	/// release it.
	/// </summary>
	/// <remarks>
	/// Counted rather than a flag: an account sends several messages concurrently, and a flag
	/// would be cleared by the first one to finish while the others are still in flight - so
	/// an edit could land in the middle of them.
	/// </remarks>
	public IDisposable Lease(int accountId)
	{
		_leaseCounts.AddOrUpdate(accountId, 1, (_, count) => count + 1);

		return new LeaseHandle(this, accountId);
	}

	private void ReleaseLease(int accountId) =>
		_leaseCounts.AddOrUpdate(accountId, 0, (_, count) => Math.Max(0, count - 1));

	public async ValueTask InvalidateAsync(int accountId)
	{
		if (_contexts.TryRemove(accountId, out var context))
		{
			// Disposed rather than merely dropped: the pool holds open, authenticated sessions
			// using credentials that are no longer current. Leaving them to the finalizer
			// would keep sending through the old password until the provider closed them.
			await context.DisposeAsync();

			_logger.LogInformation(
				"Discarded the SMTP context for sender account {AccountId}.",
				accountId);
		}
	}

	// Trimmed to the configured column width, and the provider's own words are kept: "5.4.5
	// Daily user sending limit exceeded" is what tells an operator to raise a limit rather
	// than re-enter a password.
	private static string Describe(EmailDeliveryResult result)
	{
		var text = string.IsNullOrWhiteSpace(result.StatusCode)
			? result.Message ?? result.Outcome.ToString()
			: $"{result.StatusCode} {result.Message}".Trim();

		return text.Length > 500 ? text[..500] : text;
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var context in _contexts.Values)
		{
			await context.DisposeAsync();
		}

		_contexts.Clear();

		foreach (var gate in _contextLocks.Values)
		{
			gate.Dispose();
		}

		_contextLocks.Clear();
	}

	private sealed class LeaseHandle : IDisposable
	{
		private readonly SmtpAccountPoolRegistry _registry;
		private readonly int _accountId;
		private bool _released;

		public LeaseHandle(SmtpAccountPoolRegistry registry, int accountId)
		{
			_registry = registry;
			_accountId = accountId;
		}

		public void Dispose()
		{
			if (_released)
			{
				return;
			}

			_released = true;

			_registry.ReleaseLease(_accountId);
		}
	}
}
