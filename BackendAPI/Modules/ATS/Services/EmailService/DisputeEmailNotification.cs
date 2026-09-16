namespace ATS.Services.EmailService;

/// <inheritdoc cref="IDisputeEmailNotification"/>
/// <remarks>
/// Beside the sender it uses and the internal <see cref="ATSEmailService.SendEmailForDispute"/> body
/// it is so easily confused with, so the two messages that go out for one dispute can be read side
/// by side.
/// </remarks>
public class DisputeEmailNotification : IDisputeEmailNotification
{
	private readonly ILogger<DisputeEmailNotification> _logger;
	private readonly IAtsEmailSender _emailSender;

	public DisputeEmailNotification(
		ILogger<DisputeEmailNotification> logger,
		IAtsEmailSender emailSender)
	{
		_logger = logger;
		_emailSender = emailSender;
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

		// The console sends ONE value for what the template shows as two lines: the category label
		// for Billing and Report, and free text only when "Others" is selected. Rendering both lines
		// straight from that would repeat the category as its own details, so the details bullet
		// appears only when the reason says something the category does not. Comparing the two
		// values keeps this free of any knowledge of the "Others" literal, which is a private const
		// in the Blazor component and not this service's business.
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

		var result = await _emailSender.SendATSEmailWithResultAsync(
			toEmail: details.RequestorEmail,
			subject: DisputeEmail.Subject,
			body: body,
			cancellationToken: cancellationToken,
			cc: [DisputeEmail.CopyTeam]);

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
