namespace ATS.Services.EmailService;

/// <summary>
/// A small pool of authenticated MailKit SMTP sessions, leased one at a time.
///
/// This exists because the previous implementation built a <c>System.Net.Mail.SmtpClient</c>
/// per message, so 200 invitations meant 200 TCP connects, 200 TLS handshakes and 200
/// AUTH LOGINs from a single account. Providers rate limit authentication independently of
/// send volume, and that login burst - not the message count - is what stopped this sender
/// after 14 messages. A pooled session sends many messages under one login, which is the
/// shape an ordinary mail client has.
///
/// One pool per SENDING ACCOUNT, and it must outlive any request scope - a pool that is rebuilt
/// per scope is not a pool. It is held for its account's lifetime by
/// <see cref="ATS.Services.EmailAccounts.SmtpAccountPoolRegistry"/>, which is the singleton;
/// this object itself is no longer registered in DI, because there is no longer exactly one of
/// it. Disposed when its account is deleted or its credentials change.
/// </summary>
public sealed class SmtpConnectionPool : IAsyncDisposable
{
	private readonly ILogger<SmtpConnectionPool> _logger;
	private readonly AtsEmailDeliveryOptions _options;
	private readonly SmtpRateLimiter _rateLimiter;
	private readonly SemaphoreSlim _available;
	private readonly ConcurrentBag<PooledConnection> _idle = [];
	private readonly SmtpAccountCredentials _credentials;

	// Diagnostic, and the number that matters most here: it should stay close to
	// MaxConcurrentConnections over a whole run. If it climbs with the message count, the
	// pool is not pooling and the provider is about to say so.
	private long _loginCount;

	private bool _disposed;

	public SmtpConnectionPool(
		SmtpAccountCredentials credentials,
		IOptions<AtsEmailDeliveryOptions> options,
		SmtpRateLimiter rateLimiter,
		ILogger<SmtpConnectionPool> logger)
	{
		_logger = logger;
		_options = options.Value;
		_rateLimiter = rateLimiter;
		_credentials = credentials;

		var maxConnections = _options.MaxConcurrentConnections > 0
			? _options.MaxConcurrentConnections
			: new AtsEmailDeliveryOptions().MaxConcurrentConnections;

		// Per account, not per process. Each registered mailbox gets its own budget of
		// simultaneous sessions, which is what the provider actually counts - a shared cap
		// would leave the second account idling behind the first's connections.
		_available = new SemaphoreSlim(maxConnections, maxConnections);
	}

	public int AtsEmailAccountId => _credentials.AtsEmailAccountId;

	public string SenderEmail => _credentials.EmailAddress;

	public string SenderDisplayName => _credentials.DisplayName;

	public long LoginCount => Interlocked.Read(ref _loginCount);

	/// <summary>
	/// Leases a connected, authenticated session. Dispose the lease to return it to the
	/// pool. A lease whose connection faulted is discarded rather than returned, so the
	/// next caller never inherits a half-dead session.
	/// </summary>
	public async Task<SmtpLease> AcquireAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _available.WaitAsync(cancellationToken);

		try
		{
			// Reuse an idle session when one is healthy and has budget left; otherwise
			// build a fresh one. Retire-by-count is deliberate: providers cap how long a
			// single authenticated session may live, and a very old connection is also
			// more likely to have gone silently stale behind a NAT.
			while (_idle.TryTake(out var pooled))
			{
				if (pooled.IsUsable(_options.MaxMessagesPerConnection))
				{
					return new SmtpLease(this, pooled);
				}

				await pooled.DisposeAsync();
			}

			var connection = await CreateConnectionAsync(cancellationToken);

			return new SmtpLease(this, connection);
		}
		catch
		{
			// The slot must come back even when connecting failed, or a provider outage
			// permanently shrinks the pool.
			_available.Release();
			throw;
		}
	}

	private async Task<PooledConnection> CreateConnectionAsync(CancellationToken cancellationToken)
	{
		// Refused outright rather than queued. Waiting out a 30-minute login back-off here
		// would hold a pool slot for the duration and starve the sessions that are still
		// perfectly usable.
		if (_rateLimiter.IsLoginThrottled)
		{
			throw new SmtpLoginThrottledException(
				"The SMTP provider is rate limiting authentication; no new session was opened.");
		}

		// Paced on its own budget. Authentication is throttled separately from volume at the
		// provider, and a discarded session forces a login the SEND limiter never sees -
		// which is how one bad response used to cascade into a stream of logins.
		await _rateLimiter.WaitForLoginSlotAsync(cancellationToken);

		var client = new MailKit.Net.Smtp.SmtpClient
		{
			// Applies to every network operation on this client. Generous on purpose: a
			// tight timeout fires while the provider has already accepted the message,
			// which records a false failure and triggers a duplicate send on retry.
			Timeout = (int)TimeSpan.FromSeconds(_options.SendTimeoutSeconds).TotalMilliseconds
		};

		try
		{
			await client.ConnectAsync(
				_credentials.SmtpHost,
				_credentials.SmtpPort,
				MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable,
				cancellationToken);

			await client.AuthenticateAsync(
				_credentials.EmailAddress,
				_credentials.AppPassword,
				cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			client.Dispose();

			// Classified HERE, not left to escape as an unclassified exception. "454 Too many
			// login attempts" is raised by AuthenticateAsync, so the send path's handler
			// never saw it: it was reported as a generic transient fault and retried, opening
			// yet another connection. The throttle has to be recognised at the point the
			// login happens.
			var classified = SmtpFailureClassifier.ClassifyConnectFailure(exception);

			if (classified.Outcome == EmailDeliveryOutcome.Throttled)
			{
				// The longer back-off: authentication limits are enforced over a wider
				// window than send limits, so the ten-minute send pause does not clear one.
				_rateLimiter.ReportThrottled(
					TimeSpan.FromSeconds(_options.LoginThrottleBackoffSeconds));

				_logger.LogError(
					exception,
					"The SMTP provider is rate limiting LOGINS ({StatusCode}). Pausing new sessions for {BackoffMinutes} minutes. Logins so far: {LoginCount}.",
					classified.StatusCode,
					_options.LoginThrottleBackoffSeconds / 60,
					LoginCount);
			}

			throw new SmtpConnectFailedException(classified, exception);
		}

		var totalLogins = Interlocked.Increment(ref _loginCount);

		_logger.LogInformation(
			"Opened SMTP session to {Host}:{Port} as {Sender}. Logins this process: {LoginCount}.",
			_credentials.SmtpHost,
			_credentials.SmtpPort,
			_credentials.EmailAddress,
			totalLogins);

		return new PooledConnection(client);
	}

	internal async ValueTask ReturnAsync(PooledConnection connection, bool isHealthy)
	{
		try
		{
			if (isHealthy && !_disposed && connection.IsUsable(_options.MaxMessagesPerConnection))
			{
				_idle.Add(connection);
				return;
			}

			await connection.DisposeAsync();
		}
		finally
		{
			_available.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		while (_idle.TryTake(out var pooled))
		{
			await pooled.DisposeAsync();
		}

		_available.Dispose();
	}

	/// <summary>One authenticated session plus the count of messages it has carried.</summary>
	internal sealed class PooledConnection(MailKit.Net.Smtp.SmtpClient client) : IAsyncDisposable
	{
		public MailKit.Net.Smtp.SmtpClient Client { get; } = client;

		public int MessagesSent { get; private set; }

		public void RecordSend() => MessagesSent++;

		public bool IsUsable(int maxMessages) =>
			Client.IsConnected
			&& Client.IsAuthenticated
			&& MessagesSent < maxMessages;

		public async ValueTask DisposeAsync()
		{
			// A QUIT the server never hears is not worth failing over; the socket is being
			// torn down either way.
			if (Client.IsConnected)
			{
				await SideEffectGuard.RunAsync(
					() => Client.DisconnectAsync(true),
					NullLogger.Instance,
					"close an SMTP session");
			}

			Client.Dispose();
		}
	}
}

/// <summary>
/// A borrowed SMTP session. Mark it faulted when the connection itself misbehaved, so it
/// is torn down instead of handed to the next caller.
/// </summary>
public sealed class SmtpLease : IAsyncDisposable
{
	private readonly SmtpConnectionPool _pool;
	private readonly SmtpConnectionPool.PooledConnection _connection;

	private bool _faulted;

	internal SmtpLease(SmtpConnectionPool pool, SmtpConnectionPool.PooledConnection connection)
	{
		_pool = pool;
		_connection = connection;
	}

	public MailKit.Net.Smtp.SmtpClient Client => _connection.Client;

	public void RecordSend() => _connection.RecordSend();

	public void MarkFaulted() => _faulted = true;

	public ValueTask DisposeAsync() => _pool.ReturnAsync(_connection, !_faulted);
}
