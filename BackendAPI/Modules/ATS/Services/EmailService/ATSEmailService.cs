namespace ATS.Services.EmailService;

public class ATSEmailService : IEmailService, IAtsEmailSender
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<ATSEmailService> _logger;
	private readonly ISmtpAccountPoolRegistry _poolRegistry;
	private readonly AtsEmailDeliveryOptions _options;
	private readonly int _atsApplicationFormExpirationInHours;

	public ATSEmailService(
		IConfiguration configuration,
		ILogger<ATSEmailService> logger,
		ISmtpAccountPoolRegistry poolRegistry,
		IOptions<AtsEmailDeliveryOptions> options)
	{
		_configuration = configuration;
		_logger = logger;
		_poolRegistry = poolRegistry;
		_options = options.Value;
		_atsApplicationFormExpirationInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
	}

	/// <summary>
	/// Kept for callers that only need "did it go out" - the dispute mail and the single
	/// enrolment path. The bulk processor uses <see cref="SendATSEmailWithResultAsync"/>
	/// instead, because it has to tell a rate limit apart from a bad address.
	/// </summary>
	public async Task<bool> SendATSEmailAsync(string toEmail, string subject, string body)
	{
		var result = await SendATSEmailWithResultAsync(toEmail, subject, body, CancellationToken.None);

		return result.IsSent;
	}

	/// <summary>
	/// Sends one message, moving to the next registered account when the current one is
	/// capped, throttled or rejected.
	/// </summary>
	/// <remarks>
	/// The loop is the switcher. Each iteration asks the registry for the best account that
	/// has not already refused THIS message, sends through that account's own pool and
	/// limiter, and reports the outcome back so the breaker can count it.
	///
	/// Three ways out, and the third is the subtle one:
	///
	/// A failure scoped to the message - a bad recipient - returns immediately. Trying a
	/// different sender cannot fix an address that does not exist.
	///
	/// A throttle or an auth rejection moves the message to the next account. Both are refused
	/// before the body is accepted, so re-sending delivers it exactly once.
	///
	/// A transient - socket drop, timeout - does NOT move the message, even though it counts
	/// against the account. It can fire after the provider already accepted the message, so a
	/// resend here would reliably duplicate an invitation that a candidate has already been
	/// sent. The account may still leave rotation for subsequent messages; this one goes back
	/// to the caller to defer and retry on a later pass, which is what the attempt budget is
	/// for. See <c>EmailDeliveryResult.CanRetryOnAnotherAccount</c>.
	///
	/// Bounded by the number of registered accounts, not by a retry count: once every account
	/// has refused, there is nowhere left to go and the caller must defer the row.
	/// </remarks>
	public async Task<EmailDeliveryResult> SendATSEmailWithResultAsync(
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken)
	{
		var attemptedAccountIds = new List<int>();

		// The last account-scoped failure, returned when every account has been exhausted.
		// Reporting the real provider response beats a generic "no account available": it is
		// the difference between "raise the daily limit" and "the password is wrong".
		EmailDeliveryResult? lastAccountFailure = null;

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var account = await _poolRegistry.GetNextSendableAccountAsync(
				attemptedAccountIds,
				cancellationToken);

			if (account is null)
			{
				// Always Throttled, whatever the last account actually said.
				//
				// Throttled is the only outcome that means "defer this row WITHOUT charging an
				// attempt", and that is the correct reading here however the accounts failed:
				// nothing is wrong with the recipient. Returning the last failure verbatim
				// would be a trap - three accounts with expired app passwords produce a
				// Permanent, and the processor retires perfectly valid candidate addresses on
				// the strength of our own misconfiguration.
				//
				// The reason still travels, because "raise the daily limit" and "the password
				// is wrong" need very different responses from whoever reads the log.
				return EmailDeliveryResult.Throttled(
					lastAccountFailure?.StatusCode,
					lastAccountFailure is null
						? "Every registered sender account is capped, cooling down, unverified or disabled."
						: $"Every registered sender account refused the message. Last response: {lastAccountFailure.Message}");
			}

			attemptedAccountIds.Add(account.AtsEmailAccountId);

			var result = await SendThroughAccountAsync(
				account.AtsEmailAccountId,
				toEmail,
				subject,
				body,
				cancellationToken);

			if (result.IsSent || !result.CanRetryOnAnotherAccount)
			{
				// Sent; or refused for a reason another account would refuse identically; or a
				// transient that may already have been delivered. The failure was still
				// reported to the breaker inside the send, so an unhealthy account still
				// leaves rotation - it just does not take this message with it.
				return result;
			}

			lastAccountFailure = result;

			_logger.LogWarning(
				"Sender account {AccountId} ({Email}) could not carry a message to {Recipient}: {StatusCode} {Message}. Trying the next account.",
				account.AtsEmailAccountId,
				account.EmailAddress,
				toEmail,
				result.StatusCode,
				result.Message);
		}
	}

	public async Task<EmailDeliveryResult> SendThroughAccountAsync(
		int accountId,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken)
	{
		SmtpAccountContext context;

		try
		{
			context = await _poolRegistry.GetContextAsync(accountId, cancellationToken);
		}
		catch (InvalidOperationException exception)
		{
			// The account vanished, or its stored password can no longer be decrypted. Scoped
			// to the account so the caller moves on rather than retiring the recipient.
			_logger.LogError(
				exception,
				"Could not prepare sender account {AccountId}.",
				accountId);

			return EmailDeliveryResult.Permanent(
				null,
				exception.Message,
				EmailFailureScope.Account);
		}

		// Held for the whole attempt, so an edit or delete arriving mid-send is refused rather
		// than swapping credentials underneath an open session.
		using var lease = _poolRegistry.Lease(accountId);

		var result = await SendOverContextAsync(
			context,
			toEmail,
			subject,
			body,
			cancellationToken);

		if (result.IsSent)
		{
			// One recipient per invitation today. Counted as recipients rather than messages
			// because that is what the provider counts against the daily cap.
			await _poolRegistry.ReportSuccessAsync(accountId, 1, cancellationToken);
		}
		else
		{
			// Returns whether the account left rotation; the caller does not need to know,
			// because it asks the registry for the next account either way.
			await _poolRegistry.ReportFailureAsync(accountId, result, cancellationToken);
		}

		return result;
	}

	public async Task<EmailDeliveryResult> SendWithCredentialsAsync(
		SmtpAccountCredentials credentials,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken)
	{
		// A limiter and pool of its own, disposed at the end of this call. The account has not
		// earned a place in rotation yet, so nothing here may be cached or reused - and a
		// verification send must not be paced behind a live account's queue.
		using var rateLimiter = new SmtpRateLimiter(
			Options.Create(_options),
			NullLogger<SmtpRateLimiter>.Instance);

		await using var pool = new SmtpConnectionPool(
			credentials,
			Options.Create(_options),
			rateLimiter,
			NullLogger<SmtpConnectionPool>.Instance);

		await using var context = new SmtpAccountContext(
			credentials.AtsEmailAccountId,
			credentials.DisplayName,
			credentials.EmailAddress,
			pool,
			rateLimiter);

		return await SendOverContextAsync(
			context,
			toEmail,
			subject,
			body,
			cancellationToken);
	}

	/// <summary>
	/// Sends one message over one account's pooled, already-authenticated session, pacing it
	/// through that account's rate limiter first.
	///
	/// Both of those exist because of a real incident: a per-message SmtpClient meant one
	/// AUTH LOGIN per email, and Gmail stopped this sender after 14 messages in about eight
	/// seconds. The session is now reused and the send rate is bounded, so the traffic looks
	/// like a mail client rather than a login flood.
	/// </summary>
	private async Task<EmailDeliveryResult> SendOverContextAsync(
		SmtpAccountContext context,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken)
	{
		// Paced before the connection is leased. Waiting while holding a session would idle
		// a scarce resource for no reason.
		await context.RateLimiter.WaitForSlotAsync(cancellationToken);

		SmtpLease lease;

		try
		{
			lease = await context.Pool.AcquireAsync(cancellationToken);
		}
		catch (SmtpLoginThrottledException exception)
		{
			// The pool declined to open a session because authentication is rate limited.
			// Nothing was attempted, so this is reported as a throttle rather than a
			// delivery failure - the row keeps its budget.
			return exception.Result;
		}
		catch (SmtpConnectFailedException exception)
		{
			// Already classified inside the pool, including the login-throttle case that
			// used to escape unclassified and be retried into another login.
			_logger.LogWarning(
				"Could not open an SMTP session on account {AccountId} to send to {Email}: {StatusCode} {Message}",
				context.AtsEmailAccountId,
				toEmail,
				exception.Result.StatusCode,
				exception.Result.Message);

			return exception.Result;
		}

		await using (lease)
		{
			var message = BuildMessage(context, toEmail, subject, body);

			try
			{
				await lease.Client.SendAsync(message, cancellationToken);

				lease.RecordSend();

				_logger.LogInformation("Email sent successfully to {Email}", toEmail);

				return EmailDeliveryResult.Sent;
			}
			catch (MailKit.Net.Smtp.SmtpCommandException exception)
			{
				// The server answered with a status code. The classifier also decides whether
				// the SESSION survives - a per-recipient rejection says nothing about the
				// connection, and discarding it would force a needless re-login.
				var (result, sessionIsUsable) = SmtpFailureClassifier.ClassifySendFailure(exception);

				if (!sessionIsUsable)
				{
					lease.MarkFaulted();
				}

				if (result.Outcome == EmailDeliveryOutcome.Throttled)
				{
					_logger.LogWarning(
						"SMTP throttling detected while sending to {Email}: {StatusCode} {Message}",
						toEmail,
						result.StatusCode,
						exception.Message);
				}
				else if (result.Outcome == EmailDeliveryOutcome.Permanent)
				{
					_logger.LogError(
						"Permanent SMTP rejection for {Email}: {StatusCode} {Message}",
						toEmail,
						result.StatusCode,
						exception.Message);
				}
				else
				{
					_logger.LogWarning(
						"Transient SMTP failure sending to {Email}: {StatusCode} {Message}",
						toEmail,
						result.StatusCode,
						exception.Message);
				}

				return result;
			}
			catch (MailKit.Net.Smtp.SmtpProtocolException exception)
			{
				// The conversation itself broke down. The session is not trustworthy.
				lease.MarkFaulted();

				_logger.LogError(
					exception,
					"SMTP protocol error sending to {Email}. The session was discarded.",
					toEmail);

				// Account-scoped: the conversation broke down, which says nothing about the
				// recipient. Scoping it to the message would leave this send pinned to a
				// connection that has already proven it cannot complete one.
				return EmailDeliveryResult.Transient(
					null,
					exception.Message,
					EmailFailureScope.Account);
			}
			catch (Exception exception) when (exception is IOException or SocketException or TimeoutException or OperationCanceledException
				&& !cancellationToken.IsCancellationRequested)
			{
				// Socket dropped or timed out. Transient, but the connection is dead.
				//
				// Note this can fire AFTER the provider accepted the message - which is exactly
				// how a candidate received the same invitation more than once. The generous
				// SendTimeoutSeconds default exists to make this rare rather than routine.
				lease.MarkFaulted();

				_logger.LogWarning(
					exception,
					"SMTP transport failure sending to {Email}. Treating as transient.",
					toEmail);

				// Account-scoped so the breaker counts it, but note that the failover loop does
				// NOT move a message on a lone transient - see the remark there. That matters
				// most here: this catch can fire after the provider already accepted the
				// message, so an immediate resend elsewhere would deliver it twice.
				return EmailDeliveryResult.Transient(
					null,
					exception.Message,
					EmailFailureScope.Account);
			}
		}
	}

	/// <summary>
	/// Builds the message with the From taken from the account that is about to carry it.
	/// </summary>
	/// <remarks>
	/// The From must match the authenticated mailbox. A message built with one account's address
	/// and pushed down another account's session is a spoof as far as the receiving server is
	/// concerned, and Gmail rejects it outright - which is why this takes the context rather than
	/// reading a configured sender.
	/// </remarks>
	private static MimeKit.MimeMessage BuildMessage(
		SmtpAccountContext context,
		string toEmail,
		string subject,
		string body)
	{
		var message = new MimeKit.MimeMessage();

		message.From.Add(new MimeKit.MailboxAddress(context.DisplayName, context.EmailAddress));
		message.To.Add(MimeKit.MailboxAddress.Parse(toEmail));
		message.Subject = subject;

		message.Body = new MimeKit.BodyBuilder
		{
			HtmlBody = body
		}.ToMessageBody();

		return message;
	}

	public string SendAppplicationFormNotification(string gmail, string name, string applicationFormLink, string? requestor, string? clientName)
	{
		// Older rows may predate the requestor/client columns, so the sentence
		// degrades to a generic phrasing rather than rendering an empty name.
		var requestorPhrase = string.IsNullOrWhiteSpace(requestor)
			? "The talent acquisition team"
			: WebUtility.HtmlEncode(requestor.Trim());
		var clientPhrase = string.IsNullOrWhiteSpace(clientName)
			? "their company"
			: $"{WebUtility.HtmlEncode(clientName.Trim())} company";

		string body = $@"
			<!DOCTYPE html>
			<html>
			<body style='margin:0;padding:0;background:#f4f6fb;font-family:Arial, sans-serif'>
				<div style='max-width:600px;margin:24px auto;background:#ffffff;border:1px solid #d9e5f5;border-radius:12px;overflow:hidden'>
					<div style='padding:24px 36px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-align:center'>
						<h1 style='margin:0;font-size:20px'>CIBI | Background Verification Information Request</h1>
						<p style='margin:8px 0 0;font-size:13px;line-height:1.5;color:#dbe7fb'>Pre-employment background check — please complete your application form within {_atsApplicationFormExpirationInHours} hours</p>
					</div>
					<div style='padding:34px 36px'>
						<p style='font-size:16px;line-height:1.7'>Dear {name},</p>
						<p style='font-size:16px;line-height:1.7'>
							{requestorPhrase}, talent acquisition {clientPhrase} has requested CIBI Information Inc. to perform background checks on you as part of their pre-employment screening process. Please sign up by clicking the button below:
						</p>
						<p style='margin:28px 0;text-align:center'><a href='{applicationFormLink}' style='display:inline-block;padding:14px 26px;border-radius:999px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-decoration:none;font-weight:bold'>Application Form</a></p>
						<p style='font-size:15px;line-height:1.6'>Please comply <strong>within the next {_atsApplicationFormExpirationInHours} hours upon receipt of this email</strong> so we can move forward with the completion of verification.</p>
						<p style='font-size:15px;line-height:1.6'><strong>REMINDERS IN ANSWERING THE FORM</strong></p>
						<ol style='font-size:15px;line-height:1.7;margin:0 0 16px;padding-left:20px'>
							<li>In case you do not have a SSS or TIN Number, kindly input random digits from 0 to 9 to proceed with the application.</li>
							<li>In case you have a portion to input the Email Address of HR POC, kindly input your HR person of contact on the company you are applying to.</li>
						</ol>
						<p style='font-size:15px;line-height:1.6'>
							For any questions or concerns, please do not hesitate to reach out to
							<a href='mailto:pre-workteam@cibi.com.ph' style='color:#1d5fd1'>pre-workteam@cibi.com.ph</a>
							and
							<a href='mailto:ceteam@cibi.com.ph' style='color:#1d5fd1'>ceteam@cibi.com.ph</a>
							or call us at +63 923 087 8757 (Sun), or +63 917 632 0486 (Globe).
						</p>
					</div>
					<div style='padding:20px 36px;background:#f4f8fd;color:#66788f;font-size:12px;line-height:1.6'>This e-mail and its attachments may contain sensitive and confidential information. Do not resend, copy, or use this email if you are not the intended recipient. Please contact the sender immediately and delete this entire email. The privilege is not waived because it was delivered to you mistakenly. CIBI Information Inc. and its affiliates accept no liability for any loss or harm resulting from this e-mail and reserve the right to monitor, retain, and/or review email. The opinions stated in this email are solely those of the author and may not reflect the views of CIBI Information Inc. or its affiliates.</div>
				</div>
			</body>
			</html>";

		return body;
	}

	/// <summary>
	/// The verification code for a sender account, in the ATS message format.
	/// </summary>
	/// <remarks>
	/// Static because it is called during registration, before any account exists to send it
	/// through - the management service composes it and hands it to SendWithCredentialsAsync
	/// with the credentials being proven.
	///
	/// The recipient here is an operator, not a candidate, so the copy says plainly what
	/// confirming the code will do: put this mailbox into the rotation that carries candidate
	/// invitations.
	/// </remarks>
	public static string AtsEmailAccountOtpBody(string displayName, string otpCode, int expiryInMinutes)
	{
		// The display name is operator-supplied and lands inside markup. Encoded rather than
		// trusted: this body is composed from a registration form.
		var safeName = WebUtility.HtmlEncode(
			string.IsNullOrWhiteSpace(displayName) ? "there" : displayName.Trim());

		return $@"
			<!DOCTYPE html>
			<html>
			<body style='margin:0;padding:0;background:#f4f6fb;font-family:Arial, sans-serif'>
				<div style='max-width:600px;margin:24px auto;background:#ffffff;border:1px solid #d9e5f5;border-radius:12px;overflow:hidden'>
					<div style='padding:24px 36px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-align:center'>
						<h1 style='margin:0;font-size:20px'>CIBI | Sender Email Verification</h1>
						<p style='margin:8px 0 0;font-size:13px;line-height:1.5;color:#dbe7fb'>Confirm this mailbox so the ATS can send candidate invitations through it</p>
					</div>
					<div style='padding:34px 36px'>
						<p style='font-size:16px;line-height:1.7'>Hello {safeName},</p>
						<p style='font-size:16px;line-height:1.7'>
							This mailbox is being registered as a sender account for the CIBI Applicant Tracking System.
							Enter the code below to confirm it. This message was sent using the credentials that were just
							submitted, so receiving it already proves they work.
						</p>
						<p style='margin:28px 0;text-align:center'>
							<span style='display:inline-block;padding:16px 30px;border-radius:12px;background:#f4f8fd;border:1px solid #d9e5f5;color:#0b1b3d;font-size:32px;font-weight:bold;letter-spacing:10px'>{otpCode}</span>
						</p>
						<p style='font-size:15px;line-height:1.6'>This code expires in <strong>{expiryInMinutes} minutes</strong>.</p>
						<p style='font-size:15px;line-height:1.6'>
							Once confirmed, candidate invitation emails will start going out from this address, and it will
							take its turn in the sending rotation.
						</p>
						<p style='font-size:15px;line-height:1.6'>
							<strong>If you did not expect this email</strong>, someone has entered this address and its app
							password into the ATS. Do not share the code, and revoke the app password from your mail
							provider's security settings.
						</p>
					</div>
					<div style='padding:20px 36px;background:#f4f8fd;color:#66788f;font-size:12px;line-height:1.6'>This e-mail and its attachments may contain sensitive and confidential information. Do not resend, copy, or use this email if you are not the intended recipient. Please contact the sender immediately and delete this entire email. The privilege is not waived because it was delivered to you mistakenly. CIBI Information Inc. and its affiliates accept no liability for any loss or harm resulting from this e-mail and reserve the right to monitor, retain, and/or review email. The opinions stated in this email are solely those of the author and may not reflect the views of CIBI Information Inc. or its affiliates.</div>
				</div>
			</body>
			</html>";
	}

	public string SendEmailForDispute(string gmail, string company, string disputeReason, DateTime? orderedAt, string requestor, string subjectName)
	{
		string body = $@"
			<!DOCTYPE html>
			<html>
			<body style='margin:0;padding:0;background:#f4f6fb;font-family:Arial, sans-serif'>
				<div style='max-width:600px;margin:24px auto;background:#ffffff;border:1px solid #d9e5f5;border-radius:12px;overflow:hidden'>
					<div style='padding:24px 36px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-align:center'>
						<h1 style='margin:0;font-size:20px'>CIBI | Dispute Order Notification</h1>
						<p style='margin:8px 0 0;font-size:13px;line-height:1.5;color:#dbe7fb'>A dispute has been raised on a background check order and requires your review</p>
					</div>
					<div style='padding:34px 36px'>
						<p style='font-size:16px;line-height:1.7'>Hello,</p>
						<p style='font-size:16px;line-height:1.7'>
							A request for dispute has been raised for subject
							<strong>{subjectName}</strong>.
						</p>
						<p style='font-size:15px;line-height:1.6'>Supplemental details are provided below:</p>
						<table role='presentation' style='width:100%;border-collapse:collapse;margin:24px 0;background:#f4f8fd;border:1px solid #d9e5f5;border-radius:12px'>
							<tr><td style='padding:12px 16px;color:#5b6f8f;font-size:13px'>Requestor Email:</td><td style='padding:12px 16px;font-weight:bold'>{requestor}</td></tr>
							<tr><td style='padding:12px 16px;color:#5b6f8f;font-size:13px'>Company:</td><td style='padding:12px 16px;font-weight:bold'>{company}</td></tr>
							<tr><td style='padding:12px 16px;color:#5b6f8f;font-size:13px'>Order Date:</td><td style='padding:12px 16px;font-weight:bold'>{orderedAt}</td></tr>
							<tr><td style='padding:12px 16px;color:#5b6f8f;font-size:13px'>Reason for Dispute:</td><td style='padding:12px 16px;font-weight:bold'>{disputeReason}</td></tr>
						</table>
						<p style='font-size:15px;line-height:1.6'>
							Please review the dispute request and proceed with the appropriate action.
						</p>
						<p style='font-size:15px;line-height:1.6'>Thank you.</p>
					</div>
					<div style='padding:20px 36px;background:#f4f8fd;color:#66788f;font-size:12px;line-height:1.6;text-align:center'>This is an automated notification from the ATS. Please do not reply to this email.</div>
				</div>
			</body>
			</html>";

		return body;
	}

	public string SendApprovalNotificationBody(string gmail)
	{
		throw new NotImplementedException();
	}

	public string SendNotificationBody(string gmail, string application, string submenu, string role)
	{
		throw new NotImplementedException();
	}

	public string SendOtpBody(string name, string otpCode)
	{
		throw new NotImplementedException();
	}

	public string SendPasswordResetBody(string name, string resetLink, int expireMins)
	{
		throw new NotImplementedException();
	}

	public Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
	{
		return SendATSEmailAsync(toEmail, subject, body);
	}
}
