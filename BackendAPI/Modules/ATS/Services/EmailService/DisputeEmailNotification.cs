namespace ATS.Services.EmailService;

/// <inheritdoc cref="IDisputeEmailNotification"/>
/// <remarks>
/// Beside the sender it uses and the two sibling notices, so the three messages a requestor can
/// receive about an order - withdrawn, disputed, completed - read the same way in one folder.
/// </remarks>
public class DisputeEmailNotification : IDisputeEmailNotification
{
	private readonly ILogger<DisputeEmailNotification> _logger;
	private readonly IAtsEmailSender _emailSender;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly AtsEmailDeliveryOptions _options;

	public DisputeEmailNotification(
		ILogger<DisputeEmailNotification> logger,
		IAtsEmailSender emailSender,
		IOrderHistoryService orderHistoryService,
		IOptions<AtsEmailDeliveryOptions> options)
	{
		_logger = logger;
		_emailSender = emailSender;
		_orderHistoryService = orderHistoryService;
		_options = options.Value;
	}

	public async Task SendAsync(
		DisputeEmailDetails details,
		CancellationToken cancellationToken)
	{
		// Guarded here rather than at the call site, matching WithdrawnEmailNotification and
		// AtsNotificationService. The dispute is already committed by the time this runs, so a
		// failure reaching CustomExceptionHandler would answer a filed dispute with a 500 and the
		// filer would submit it again.
		await SideEffectGuard.RunAsync(
			() => SendNoticeAsync(details, cancellationToken),
			_logger,
			$"acknowledge the dispute filed by {details.RequestorEmail}",
			cancellationToken);
	}

	/// <summary>
	/// Resolves the two body lines, composes and sends. Throws freely: its only caller is wrapped
	/// in <see cref="SideEffectGuard"/>, which logs and swallows.
	/// </summary>
	private async Task SendNoticeAsync(
		DisputeEmailDetails details,
		CancellationToken cancellationToken)
	{
		// The address comes from a token claim, so it is missing on a token issued before the claim
		// existed. There is nobody to acknowledge to - and the dispute itself is already recorded,
		// which is the part that actually matters.
		if (string.IsNullOrWhiteSpace(details.RequestorEmail))
		{
			_logger.LogWarning(
				"A dispute was recorded for {CandidateName} but the submitting user has no email claim, so no acknowledgement was sent.",
				details.CandidateName);

			return;
		}

		// The console now sends both lines separately - a category label and the free text every
		// category requires - so the usual path renders both. Two cases still collapse to one line:
		// a client that predates the split and sent only the label in DisputeReason, and a filer who
		// typed the category name into the description. Both would otherwise read "Category: Report
		// / Details: Report". Comparing the two values covers both without this service needing to
		// know which categories exist.
		var category = string.IsNullOrWhiteSpace(details.DisputeCategory)
			? details.DisputeReason
			: details.DisputeCategory;

		var hasSeparateDetails =
			!string.IsNullOrWhiteSpace(details.DisputeReason)
			&& !string.Equals(details.DisputeReason, category, StringComparison.Ordinal);

		// Falls back to the address: the copy reads "Dear <name>," and an address still identifies
		// the recipient where an empty name would leave "Dear ,".
		var requestorName = string.IsNullOrWhiteSpace(details.RequestorName)
			? details.RequestorEmail
			: details.RequestorName;

		var body = _emailSender.BuildDisputeNotification(
			requestorName,
			details.CandidateName,
			category,
			hasSeparateDetails ? details.DisputeReason : null);

		// Only the send is inside the retry - the body above is composed once, so a second attempt
		// re-sends the same acknowledgement rather than rebuilding it.
		var result = await SingleEmailSendRetry.SendAsync(
			send: _ => _emailSender.SendATSEmailWithResultAsync(
				toEmail: details.RequestorEmail,
				subject: DisputeEmail.Subject,
				body: body,
				cancellationToken: cancellationToken,
				cc: [DisputeEmail.CopyTeam]),
			maxAttempts: _options.MaxAttemptsPerMessage,
			baseDelaySeconds: _options.RetryBaseDelaySeconds,
			logger: _logger,
			description: $"the dispute acknowledgement for order {details.EmailInvitationId}",
			cancellationToken: cancellationToken);

		// Records the ATTEMPT rather than the delivery, so a failed send still leaves a row and the
		// outcome stays in the log below. Status written unchanged - a dispute does not move the
		// order - and Completed matches the fallback the ReportDisputed row beside it uses, since a
		// report can only be disputed once it exists.
		await _orderHistoryService.RecordAsync(
			details.EmailInvitationId,
			OrderHistoryEventType.DisputeAcknowledgementEmail,
			null,
			OrderStatus.Completed,
			cancellationToken);

		if (!result.IsSent)
		{
			_logger.LogWarning(
				"Dispute acknowledgement was not delivered to {RequestorEmail} for candidate {CandidateName}: {StatusCode} {Message}",
				details.RequestorEmail,
				details.CandidateName,
				result.StatusCode,
				result.Message);

			return;
		}

		_logger.LogInformation(
			"Dispute acknowledgement sent to {RequestorEmail} for candidate {CandidateName}, category {DisputeCategory}.",
			details.RequestorEmail,
			details.CandidateName,
			category);
	}
}
