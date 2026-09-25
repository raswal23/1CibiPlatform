namespace ATS.Services.EmailService;

/// <inheritdoc cref="ISubmittedFormEmailNotification"/>
/// <remarks>
/// Beside the sender it uses and the two sibling notices it completes the set with - withdrawal,
/// dispute and completion are the three things a candidate can do to an order that the requestor
/// needs to hear about.
/// </remarks>
public class SubmittedFormEmailNotification : ISubmittedFormEmailNotification
{
	private readonly ILogger<SubmittedFormEmailNotification> _logger;
	private readonly IAtsEmailSender _emailSender;
	private readonly IAuthQueries _authQueries;
	private readonly IATSRepository _atsRepository;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly IEmailProcessManagementService _emailProcessManagementService;
	private readonly AtsEmailDeliveryOptions _options;

	public SubmittedFormEmailNotification(
		ILogger<SubmittedFormEmailNotification> logger,
		IAtsEmailSender emailSender,
		IAuthQueries authQueries,
		IATSRepository atsRepository,
		IOrderHistoryService orderHistoryService,
		IEmailProcessManagementService emailProcessManagementService,
		IOptions<AtsEmailDeliveryOptions> options)
	{
		_logger = logger;
		_emailSender = emailSender;
		_authQueries = authQueries;
		_atsRepository = atsRepository;
		_orderHistoryService = orderHistoryService;
		_emailProcessManagementService = emailProcessManagementService;
		_options = options.Value;
	}

	public async Task SendAsync(
		SubmittedFormEmailDetails details,
		CancellationToken cancellationToken)
	{
		// Guarded here rather than at the call site, matching the other two notices. Beyond the
		// usual "the work is already durable" reasoning, this caller's catch block deletes the
		// uploaded attachments as compensation - so an exception escaping after the commit would
		// destroy the files belonging to a form that is already saved.
		await SideEffectGuard.RunAsync(
			() => SendNoticeAsync(details, cancellationToken),
			_logger,
			$"notify the requestor that the application form for order {details.EmailInvitationId} was completed",
			cancellationToken);
	}

	/// <summary>
	/// Loads the order, resolves the requestor, composes and sends. Throws freely: its only caller
	/// is wrapped in <see cref="SideEffectGuard"/>, which logs and swallows.
	/// </summary>
	private async Task SendNoticeAsync(
		SubmittedFormEmailDetails details,
		CancellationToken cancellationToken)
	{
		// The caller has only the order id, so the row is read here - the same shape as
		// AtsNotificationService.RaiseForOrderAsync, and through the cached decorator. Note the
		// repository returns an EMPTY placeholder rather than null when the id matches nothing, so
		// a missing row arrives here looking like a row with no requestor and no address; the guard
		// below covers both, which is why there is no separate null check.
		var invitation = await _atsRepository.GetEmailInvitationRequestByIdAsync(
			details.EmailInvitationId,
			cancellationToken);

		if (!invitation.RequestorId.HasValue)
		{
			_logger.LogWarning(
				"Application form for order {EmailInvitationID} was submitted but the order has no requestor id, so no completion notice was sent.",
				details.EmailInvitationId);

			return;
		}

		var requestor = await _authQueries.GetATSAssignedUserAsync(
			invitation.RequestorId.Value,
			cancellationToken);

		if (string.IsNullOrWhiteSpace(requestor?.UserEmail))
		{
			_logger.LogWarning(
				"Application form for order {EmailInvitationID} was submitted but requestor {RequestorId} has no ATS user email, so no completion notice was sent.",
				details.EmailInvitationId,
				invitation.RequestorId.Value);

			return;
		}

		// The directory's joined name is authoritative; the name stored on the order is the fallback
		// for a user who has since lost the ATS assignment this lookup filters on.
		var requestorName = string.IsNullOrWhiteSpace(requestor.UserName)
			? invitation.Requestor ?? requestor.UserEmail
			: requestor.UserName;

		var body = _emailSender.BuildSubmittedFormNotification(
			requestorName,
			ResolveCandidateName(details.CandidateName, invitation));

		// The CIBI teams copied on this notice come from its EmailProcessDetails row, so the list is
		// an operator's to change. The candidate joins them from the address the invitation link was
		// sent to - the submitted form carries no primary email address, only an alternative one, so
		// the order row is the only reliable mailbox for them. An order with no address simply leaves
		// the candidate off the copy rather than failing the send.
		var cc = new List<string>(
			await _emailProcessManagementService.GetCopyListAsync(
				AtsEmailProcess.SubmittedForm,
				cancellationToken));

		if (!string.IsNullOrWhiteSpace(invitation.EmailAddress))
		{
			cc.Add(invitation.EmailAddress);
		}

		// Only the send is inside the retry. The order read, the directory lookup and the copy list
		// above are resolved once, so a second attempt re-sends the same message rather than
		// rebuilding it.
		var result = await SingleEmailSendRetry.SendAsync(
			send: _ => _emailSender.SendATSEmailWithResultAsync(
				toEmail: requestor.UserEmail,
				subject: SubmittedFormEmail.Subject,
				body: body,
				cancellationToken: cancellationToken,
				cc: cc),
			maxAttempts: _options.MaxAttemptsPerMessage,
			baseDelaySeconds: _options.RetryBaseDelaySeconds,
			logger: _logger,
			description: $"the completion notice for order {details.EmailInvitationId}",
			cancellationToken: cancellationToken);

		// Records the ATTEMPT rather than the delivery, so a failed send still leaves a row and the
		// outcome stays in the log below. Status written unchanged, because emailing is not a step
		// in the order's lifecycle; see OrderHistoryEventType for why this is its own event type
		// rather than folded into ApplicationFormSubmitted.
		await _orderHistoryService.RecordAsync(
			details.EmailInvitationId,
			OrderHistoryEventType.CompletionNoticeEmail,
			null,
			OrderStatus.InProgress,
			cancellationToken);

		if (!result.IsSent)
		{
			_logger.LogWarning(
				"Completion notice for order {EmailInvitationID} was not delivered to {RequestorEmail}: {StatusCode} {Message}",
				details.EmailInvitationId,
				requestor.UserEmail,
				result.StatusCode,
				result.Message);

			return;
		}

		_logger.LogInformation(
			"Completion notice for order {EmailInvitationID} sent to {RequestorEmail} with {CcCount} copied.",
			details.EmailInvitationId,
			requestor.UserEmail,
			cc.Count);
	}

	/// <summary>
	/// The name to put in the body: what the candidate typed, then what the order holds, then their
	/// mailbox.
	/// </summary>
	/// <remarks>
	/// The submitted name wins because this notice is about the form's contents, and a candidate who
	/// corrected a typo in their own name should see the correction. A form can still be submitted
	/// with the name fields blank, and the copy reads "Your candidate, &lt;name&gt;, has successfully
	/// completed the Application Form" - so the last resort is the address the invitation went to,
	/// which identifies a person to the requestor where a blank would leave a hole mid-sentence.
	/// </remarks>
	private static string ResolveCandidateName(
		string? submittedName,
		EmailInvitationRequest invitation)
	{
		if (!string.IsNullOrWhiteSpace(submittedName))
		{
			return submittedName.Trim();
		}

		var storedName = $"{invitation.FirstName} {invitation.LastName}".Trim();

		return string.IsNullOrWhiteSpace(storedName)
			? invitation.EmailAddress ?? "the candidate"
			: storedName;
	}
}
