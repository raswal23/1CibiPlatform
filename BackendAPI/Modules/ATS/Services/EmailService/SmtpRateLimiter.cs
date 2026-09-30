namespace ATS.Services.EmailService;

/// <summary>
/// A token bucket over one SENDING ACCOUNT: no more than <c>MaxSendsPerSecond</c> messages
/// leave this application through that account per second, no matter how many connections or
/// Quartz passes are running.
///
/// Why a limiter as well as a connection cap: the two bound different things. The cap
/// bounds how many SMTP sessions exist; the limiter bounds how fast messages flow through
/// them. Without it, N pooled connections send as fast as the network allows and the
/// provider sees a burst - which is precisely what got this sender throttled at 14
/// messages. With it, raising the connection count improves latency-hiding without ever
/// raising the send rate past what the provider tolerates.
///
/// Held for the lifetime of its account by
/// <see cref="ATS.Services.EmailAccounts.SmtpAccountPoolRegistry"/>, one instance per account,
/// and NOT registered in DI. The rule it used to state - "singleton, because a per-scope
/// limiter would let two concurrent passes each send at the full rate" - still holds exactly
/// as written; what changed is only that "the sending account" is no longer a synonym for
/// "the process". Scoping this per request would still double the real rate.
///
/// Deliberately unchanged otherwise. Its single <c>_throttledUntilUtc</c> was already correct
/// WITHIN one account: Gmail throttling one mailbox says nothing about another, and now that
/// each mailbox owns its own limiter, "stop everything" means "stop this account" - which is
/// what the switcher needs in order to move on.
/// </summary>
public sealed class SmtpRateLimiter : IDisposable
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly ILogger<SmtpRateLimiter> _logger;
	private readonly double _minIntervalTicks;
	private readonly TimeSpan _minLoginInterval;

	private DateTime _nextSlotUtc = DateTime.MinValue;
	private DateTime _nextLoginSlotUtc = DateTime.MinValue;

	// Set when the provider says "slow down". Every waiter parks until it passes, which is
	// what stops a pass from spending its whole retry budget against a closed door.
	private DateTime _throttledUntilUtc = DateTime.MinValue;

	public SmtpRateLimiter(
		IOptions<AtsEmailDeliveryOptions> options,
		ILogger<SmtpRateLimiter> logger)
	{
		_logger = logger;

		var perSecond = options.Value.MaxSendsPerSecond;

		// A non-positive rate would mean "never send", which is never what was meant -
		// treat it as unconfigured and fall back to the documented default.
		if (perSecond <= 0)
		{
			perSecond = new AtsEmailDeliveryOptions().MaxSendsPerSecond;
		}

		_minIntervalTicks = TimeSpan.TicksPerSecond / perSecond;

		_minLoginInterval = TimeSpan.FromSeconds(
			Math.Max(0, options.Value.MinSecondsBetweenLogins));
	}

	/// <summary>
	/// Blocks until this caller is allowed to send. Returns when a slot is due; the caller
	/// sends immediately afterwards.
	/// </summary>
	public async Task WaitForSlotAsync(CancellationToken cancellationToken)
	{
		TimeSpan delay;

		// The reservation is taken under the lock, but the WAIT happens outside it.
		// Sleeping while holding the gate would serialise every sender behind one another
		// and collapse the concurrency the connection pool exists to provide.
		await _gate.WaitAsync(cancellationToken);

		try
		{
			var now = DateTime.UtcNow;

			// A live throttle outranks the normal cadence: the next slot moves to the end
			// of the back-off rather than one interval from now.
			var earliest = _throttledUntilUtc > now ? _throttledUntilUtc : now;

			if (_nextSlotUtc < earliest)
			{
				_nextSlotUtc = earliest;
			}

			var slot = _nextSlotUtc;

			_nextSlotUtc = slot.AddTicks((long)_minIntervalTicks);

			delay = slot - now;
		}
		finally
		{
			_gate.Release();
		}

		if (delay > TimeSpan.Zero)
		{
			await Task.Delay(delay, cancellationToken);
		}
	}

	/// <summary>
	/// Blocks until a NEW authenticated session may be opened.
	///
	/// Paced separately from sends, and this is not a refinement - it is the fix for a real
	/// failure. Authentication has its own budget at the provider ("454 Too many login
	/// attempts" arrives long before any complaint about volume), and a discarded session
	/// forces a login that the send limiter never sees. Without this gate, one throttled
	/// send could produce an unbounded stream of logins, each one provoking the next
	/// throttle.
	/// </summary>
	public async Task WaitForLoginSlotAsync(CancellationToken cancellationToken)
	{
		TimeSpan delay;

		await _gate.WaitAsync(cancellationToken);

		try
		{
			var now = DateTime.UtcNow;

			var earliest = _throttledUntilUtc > now ? _throttledUntilUtc : now;

			if (_nextLoginSlotUtc < earliest)
			{
				_nextLoginSlotUtc = earliest;
			}

			var slot = _nextLoginSlotUtc;

			_nextLoginSlotUtc = slot.Add(_minLoginInterval);

			delay = slot - now;
		}
		finally
		{
			_gate.Release();
		}

		if (delay > TimeSpan.Zero)
		{
			_logger.LogInformation(
				"Delaying a new SMTP login by {DelaySeconds:0.0}s to stay under the provider's authentication limit.",
				delay.TotalSeconds);

			await Task.Delay(delay, cancellationToken);
		}
	}

	/// <summary>
	/// Records that the provider is rate limiting this sender. Every subsequent
	/// <see cref="WaitForSlotAsync"/> parks until the back-off elapses.
	/// </summary>
	public void ReportThrottled(TimeSpan backoff)
	{
		var until = DateTime.UtcNow.Add(backoff);

		_gate.Wait();

		try
		{
			// Never shorten an existing throttle: two workers hitting the limit at once
			// must not let the second one's shorter window undo the first one's.
			if (until > _throttledUntilUtc)
			{
				_throttledUntilUtc = until;
			}
		}
		finally
		{
			_gate.Release();
		}

		_logger.LogWarning(
			"SMTP provider is rate limiting this sender. Pausing all sends until {ThrottledUntil:O}.",
			until);
	}

	/// <summary>
	/// True while a provider throttle is in force, without the side effect of taking a slot.
	/// Used by the pool to refuse a new login outright rather than queue behind the
	/// back-off holding a pool slot for the duration.
	/// </summary>
	public bool IsLoginThrottled => DateTime.UtcNow < _throttledUntilUtc;

	/// <summary>
	/// True while a provider throttle is in force. The processor reads this to abandon the
	/// rest of a pass instead of queueing every remaining address behind the back-off.
	/// </summary>
	public bool IsThrottled => DateTime.UtcNow < _throttledUntilUtc;

	public void Dispose() => _gate.Dispose();
}
