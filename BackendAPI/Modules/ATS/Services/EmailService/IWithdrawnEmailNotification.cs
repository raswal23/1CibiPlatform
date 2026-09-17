namespace ATS.Services.EmailService;

/// <summary>
/// Announces a candidate's withdrawal of their own application form to the requestor who raised
/// the order.
/// </summary>
/// <remarks>
/// Best-effort by contract, in the same sense as <c>IAtsNotificationService</c>: every caller is
/// finishing work that has already committed, so an implementation must never let a delivery
/// failure escape. Callers await it after their commit and do not wrap it themselves.
/// </remarks>
public interface IWithdrawnEmailNotification
{
	/// <summary>
	/// Sends the withdrawal notice for an order that has just been withdrawn. Silently does nothing
	/// when the order has no requestor to address.
	/// </summary>
	Task SendAsync(
		EmailInvitationRequest invitation,
		CancellationToken cancellationToken);
}
