namespace ATS.Services.EmailService;

/// <inheritdoc cref="IWithdrawnEmailNotification"/>
/// <remarks>
/// Lives beside the sender it uses rather than inside <c>ApplicationFormService</c>, so the whole
/// withdrawal notice - who it goes to, how the mailbox is resolved, what it says - reads in one
/// place next to <see cref="ATSEmailService"/> and <see cref="WithdrawnEmail"/>.
/// </remarks>
public class WithdrawnEmailNotification : IWithdrawnEmailNotification
{
	private readonly ILogger<WithdrawnEmailNotification> _logger;
	private readonly IAtsEmailSender _emailSender;
	private readonly IAuthQueries _authQueries;

	public WithdrawnEmailNotification(
		ILogger<WithdrawnEmailNotification> logger,
		IAtsEmailSender emailSender,
		IAuthQueries authQueries)
	{
		_logger = logger;
		_emailSender = emailSender;
		_authQueries = authQueries;
	}

	public async Task SendAsync(
		EmailInvitationRequest invitation,
		CancellationToken cancellationToken)
	{
		// Guarded here rather than at the call site, matching AtsNotificationService. If this threw
		// and reached CustomExceptionHandler the candidate would get a 500 for a withdrawal that
		// already committed - and would read it as "my withdrawal failed".
		await SideEffectGuard.RunAsync(
			() => SendNoticeAsync(invitation, cancellationToken),
			_logger,
			$"notify the requestor that invitation {invitation.EmailInvitationID} was withdrawn",
			cancellationToken);
	}

	/// <summary>
	/// Resolves the requestor, composes the body and sends. Throws freely: its only caller is
	/// wrapped in <see cref="SideEffectGuard"/>, which logs and swallows.
	/// </summary>
	/// <remarks>
	/// Addressed to the requestor rather than the candidate because the candidate is the one who
	/// pressed Withdraw - they are copied so the notice is on record for them, not to tell them
	/// something they just did. The requestor is the one who has to stop the verification.
	///
	/// The requestor's ADDRESS is not on the order. <c>EmailInvitationRequest.Requestor</c> holds a
	/// display name and <c>RequestorId</c> an Auth user id, so the mailbox comes from the Auth
	/// directory - the same lookup the OMS ticketing processor makes, cached behind
	/// <c>AuthCacheRepository</c>.
	/// </remarks>
	private async Task SendNoticeAsync(
		EmailInvitationRequest invitation,
		CancellationToken cancellationToken)
	{
		// Orders raised through the public API carry no requestor id at all, so there is nobody to
		// address. Logged and skipped rather than thrown - the withdrawal already committed, and
		// nothing about it is wrong.
		if (!invitation.RequestorId.HasValue)
		{
			_logger.LogWarning(
				"Order {EmailInvitationID} was withdrawn but has no requestor id, so no withdrawal notice was sent.",
				invitation.EmailInvitationID);

			return;
		}

		var requestor = await _authQueries.GetATSAssignedUserAsync(
			invitation.RequestorId.Value,
			cancellationToken);

		if (string.IsNullOrWhiteSpace(requestor?.UserEmail))
		{
			_logger.LogWarning(
				"Order {EmailInvitationID} was withdrawn but requestor {RequestorId} has no ATS user email, so no withdrawal notice was sent.",
				invitation.EmailInvitationID,
				invitation.RequestorId.Value);

			return;
		}

		// The directory's joined name is authoritative; the name stored on the order is the fallback
		// for a user who has since lost the ATS assignment this lookup filters on.
		var requestorName = string.IsNullOrWhiteSpace(requestor.UserName)
			? invitation.Requestor ?? requestor.UserEmail
			: requestor.UserName;

		var body = _emailSender.BuildWithdrawnApplicationNotification(
			requestorName,
			BuildCandidateName(invitation));

		// A row with no candidate address simply leaves them off the copy. The notice is still worth
		// sending to the requestor, who is the one that has to stop the verification.
		var cc = new List<string> { WithdrawnEmail.CopyTeam };

		if (!string.IsNullOrWhiteSpace(invitation.EmailAddress))
		{
			cc.Add(invitation.EmailAddress);
		}

		var result = await _emailSender.SendATSEmailWithResultAsync(
			toEmail: requestor.UserEmail,
			subject: WithdrawnEmail.Subject,
			body: body,
			cancellationToken: cancellationToken,
			cc: cc);

		if (!result.IsSent)
		{
			_logger.LogWarning(
				"Withdrawal notice for order {EmailInvitationID} was not delivered to {RequestorEmail}: {StatusCode} {Message}",
				invitation.EmailInvitationID,
				requestor.UserEmail,
				result.StatusCode,
				result.Message);

			return;
		}

		_logger.LogInformation(
			"Withdrawal notice for order {EmailInvitationID} sent to {RequestorEmail} with {CcCount} copied.",
			invitation.EmailInvitationID,
			requestor.UserEmail,
			cc.Count);
	}

	private static string BuildCandidateName(EmailInvitationRequest invitation)
	{
		var name = $"{invitation.FirstName} {invitation.LastName}".Trim();

		// Falls back to the address the invitation was sent to. The copy reads "Your candidate,
		// <name>, has withdrawn", and an address still identifies the person to the requestor where
		// an empty name would not.
		return string.IsNullOrWhiteSpace(name)
			? invitation.EmailAddress ?? "the candidate"
			: name;
	}
}
