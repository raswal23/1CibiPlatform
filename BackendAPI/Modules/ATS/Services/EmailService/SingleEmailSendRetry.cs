namespace ATS.Services.EmailService;

/// <summary>
/// Retries ONE message while the fault is transient, backing off between attempts.
/// </summary>
/// <remarks>
/// SINGLE, as opposed to <c>BulkEmailNotificationProcessorService</c>: this is the attempt budget
/// for one message a caller is sending right now, not for a queued row. The queue's retry is its
/// next tick, which costs nothing and holds no worker; a caller standing in a transaction has no
/// next tick, so it spends its attempts in place.
///
/// It wraps <c>ATSEmailService.SendATSEmailWithResultAsync</c> - the switcher - from the OUTSIDE,
/// so one attempt is one full walk of the registered accounts. The switcher's own exits mean that
/// walk is short for everything except the outcome this retries:
///
/// A throttle or a rejected credential never reaches here as itself. The switcher moves the
/// message to the next account on the spot, and only reports Throttled once EVERY account has
/// refused - at which point re-knocking is the one response that makes it worse. See
/// <c>SmtpAccountPoolRegistry.ReportFailureAsync</c>, which puts an account into cooldown on the
/// first throttle for exactly that reason.
///
/// A Permanent scoped to the MESSAGE is a refused recipient. The server has already read the
/// address and said no; a second attempt produces the identical refusal, and another account
/// refuses the same address the same way.
///
/// A Transient - a dropped socket, a timeout - is the only one worth asking twice, and the
/// switcher deliberately does NOT move it to another account
/// (<c>EmailDeliveryResult.CanRetryOnAnotherAccount</c>), so it arrives here as the final answer
/// with the highest-priority account still selected. The retry therefore lands on that same account
/// again, unless its consecutive failures have meanwhile tripped
/// <c>ConsecutiveFailureThreshold</c> and retired it - in which case the next attempt walks to the
/// account behind it. That is the intended shape: stay put while it looks like noise, move on once
/// it looks like a pattern, and let the breaker rather than this loop decide which it is.
///
/// KNOWN RISK, accepted deliberately: a transient can fire AFTER the provider already accepted the
/// message - a socket drop or a timeout on a send that landed. See the comment on
/// <c>AtsEmailDeliveryOptions.SendTimeoutSeconds</c>, which records that this once sent a candidate
/// the same invitation several times. Retrying here can therefore duplicate a delivered message. A
/// duplicate was judged cheaper than a requestor never hearing that their candidate withdrew, but
/// three attempts is the whole budget - it does not grow with the number of registered accounts.
///
/// Static and stateless, reading no configuration: the caller supplies both bounds from
/// <c>AtsEmailDeliveryOptions</c>. Separate from its call sites purely so the attempt loop can be
/// tested with scripted results and no SMTP server.
/// </remarks>
public static class SingleEmailSendRetry
{
	/// <summary>
	/// Invokes <paramref name="send"/> until it reports anything other than a transient fault or
	/// the attempts run out, and returns the last result either way.
	/// </summary>
	/// <param name="send">
	/// Receives the 1-based attempt number, so a caller can log which attempt it is on. Must be the
	/// send itself and nothing else - it runs up to <paramref name="maxAttempts"/> times, so
	/// composing the body, resolving a mailbox or building a copy list belongs outside it.
	/// </param>
	/// <param name="maxAttempts">
	/// Attempts for THIS message before the caller is told it failed. Floored at one: a configured
	/// zero must not mean "never send".
	/// </param>
	/// <param name="baseDelaySeconds">
	/// The first back-off, doubling per attempt. Exponential rather than fixed, because re-knocking
	/// at a constant interval is what a provider reads as a client that will not take no for an
	/// answer.
	/// </param>
	/// <param name="description">
	/// What is being sent, for the retry log line. Reads as "to send {description}".
	/// </param>
	public static async Task<EmailDeliveryResult> SendAsync(
		Func<int, Task<EmailDeliveryResult>> send,
		int maxAttempts,
		int baseDelaySeconds,
		ILogger logger,
		string description,
		CancellationToken cancellationToken)
	{
		var attemptCeiling = Math.Max(1, maxAttempts);

		for (var attempt = 1; ; attempt++)
		{
			var result = await send(attempt);

			// Sent, a refused recipient, or an exhausted account rotation all leave immediately.
			// Only a transient is worth sending twice.
			if (result.Outcome != EmailDeliveryOutcome.Transient || attempt >= attemptCeiling)
			{
				return result;
			}

			var backoff = TimeSpan.FromSeconds(baseDelaySeconds * Math.Pow(2, attempt - 1));

			logger.LogWarning(
				"Attempt {Attempt} of {MaxAttempts} to send {Description} failed transiently: {StatusCode} {Message}. Retrying in {BackoffSeconds}s.",
				attempt,
				attemptCeiling,
				description,
				result.StatusCode,
				result.Message,
				backoff.TotalSeconds);

			await Task.Delay(backoff, cancellationToken);
		}
	}
}
