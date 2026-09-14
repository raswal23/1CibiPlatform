namespace ATS.Configuration;

/// <summary>
/// SMTP throughput and back-off tuning, mirroring AtsNotificationOptions. Every value has a
/// working default, so an absent section is valid; bind the "AtsEmailDelivery" section only
/// to override one.
///
/// These exist as configuration rather than constants because the safe ceiling belongs to
/// the provider, not to the code. Gmail throttled this sender at 14 messages, and finding
/// the sustainable rate for a new provider must not require a redeploy.
/// </summary>
public sealed class AtsEmailDeliveryOptions
{
	public const string SectionName = "AtsEmailDelivery";

	// Connections, not messages. Each one is authenticated once and then reused for many
	// sends, so this is the number of simultaneous SMTP sessions - which is what a provider
	// actually counts. Two is deliberately conservative for Gmail; a transactional provider
	// (SES, SendGrid) will happily take far more.
	public int MaxConcurrentConnections { get; set; } = 2;

	// Messages per second across ALL connections. The token bucket enforces this globally,
	// so raising MaxConcurrentConnections alone cannot outrun the provider.
	//
	// Gmail accepted 14 messages in roughly 8 seconds (~1.75/s) before refusing. Half that
	// is the sustainable rate, and it still clears 200 invitations in about three minutes.
	public double MaxSendsPerSecond { get; set; } = 0.9;

	// How many messages one authenticated session sends before it is torn down and rebuilt.
	// Providers cap the lifetime of a single session, and a very long-lived connection is
	// also more likely to have gone silently stale.
	public int MaxMessagesPerConnection { get; set; } = 50;

	// Generous on purpose. A 10s timeout was firing while the provider had ALREADY accepted
	// the message, so the send was recorded as failed and retried - which is how one
	// candidate received the same invitation several times.
	public int SendTimeoutSeconds { get; set; } = 60;

	// Attempts within one pass, before the row goes back to the queue for a later tick.
	public int MaxAttemptsPerPass { get; set; } = 3;

	// First retry waits this long, doubling per attempt (2s, 4s, 8s...). Fixed short delays
	// re-knock on a door that is deliberately closed.
	public int RetryBaseDelaySeconds { get; set; } = 2;

	// When the provider answers "slow down" (SMTP 4xx), the whole pass stops for this long.
	// Continuing to send into a rate limit is what turns a short throttle into a long one.
	public int ThrottleBackoffSeconds { get; set; } = 600;

	// Minimum gap between opening NEW authenticated sessions, paced separately from sends.
	//
	// Providers throttle authentication on its own budget - Gmail answers "454 Too many
	// login attempts" long before it complains about message volume. Pacing messages does
	// nothing for that, because a discarded session forces a fresh login that the send
	// limiter never sees. Five seconds means a pool of 2 refills in ten, and a pathological
	// reconnect loop still cannot exceed 12 logins a minute.
	public int MinSecondsBetweenLogins { get; set; } = 5;

	// A login throttle ("454 Too many login attempts") is answered with a longer pause than
	// a send throttle. Authentication limits are enforced over a longer window, so the
	// ten-minute send back-off is not enough to clear one.
	public int LoginThrottleBackoffSeconds { get; set; } = 1_800;

	// How many CONSECUTIVE transient failures retire a sender account and move the queue to the
	// next one. Three, because one is noise - a dropped socket happens - and waiting for ten
	// would spend ten messages' worth of latency discovering what the third already told us.
	//
	// Only transient failures reach this counter. A 550 is about the candidate's address, not
	// the account, and counting it would burn every registered account on one bad batch.
	public int ConsecutiveFailureThreshold { get; set; } = 3;

	// How long a tripped breaker keeps an account out of rotation. Fifteen minutes is long
	// enough for a transient provider fault to clear and short enough that a two-account setup
	// is not left on one sender for the rest of the day.
	public int TransientFailureCooldownSeconds { get; set; } = 900;

	// Applied to a newly registered account when the operator does not set one. Below Gmail's
	// ~500/day on purpose: the account has to leave rotation BEFORE the provider refuses,
	// because a refusal locks the mailbox for roughly 24 hours.
	public int DefaultDailySendLimit { get; set; } = 450;

	// The window the consumption figure is counted over. Twenty-four rolling hours, because
	// that is how the provider enforces it - a send at 23:00 still counts at 22:00 the next
	// day, so a counter reset at any fixed hour would overstate the headroom.
	public int QuotaWindowHours { get; set; } = 24;

	// How long a successful send's log row is kept. Nothing reads past QuotaWindowHours; the
	// extra day exists so a clock skew or a paused sweep cannot erase a live window.
	public int SendLogRetentionHours { get; set; } = 48;
}
