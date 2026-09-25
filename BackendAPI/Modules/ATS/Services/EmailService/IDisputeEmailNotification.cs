namespace ATS.Services.EmailService;

/// <summary>
/// Acknowledges a dispute to the person who filed it.
/// </summary>
/// <remarks>
/// Best-effort by contract, in the same sense as <see cref="IWithdrawnEmailNotification"/> and
/// <c>IAtsNotificationService</c>: the caller has already committed the dispute, so an
/// implementation must never let a delivery failure escape. Callers await it after their commit and
/// do not wrap it themselves.
///
/// This is the only email a dispute produces. It replaced an earlier internal operations alert that
/// went to the <c>ATS:DisputeOrderEmailRecipient</c> mailbox before the transaction and threw when it
/// could not be delivered — which meant an SMTP outage stopped disputes from being filed at all.
/// That coupling is gone: the dispute is recorded first and this acknowledgement is best-effort. CIBI
/// now learns of a dispute from the addresses copied on this message rather than from a separate one
/// - which are whatever the <see cref="AtsEmailProcess.Dispute"/> row holds, so switching that row
/// off means nobody at CIBI is told a dispute was filed.
/// </remarks>
public interface IDisputeEmailNotification
{
	/// <summary>
	/// Sends the acknowledgement. Silently does nothing when the filer has no address to send to.
	/// </summary>
	Task SendAsync(
		DisputeEmailDetails details,
		CancellationToken cancellationToken);
}

/// <summary>
/// What the acknowledgement needs, gathered by the caller while it still holds both the order and
/// the authenticated user.
/// </summary>
/// <remarks>
/// Values rather than the order entity, because the requestor here is the person who pressed Send
/// Dispute — read from the token — and not necessarily the order's <c>RequestorId</c>. No single
/// object carries both.
///
/// <paramref name="DisputeCategory"/> and <paramref name="DisputeReason"/> are passed through as
/// the client sent them, collapsing and all; deciding what the two body lines should say from that
/// is this feature's business, not the caller's. See <see cref="DisputeEmailNotification"/>.
///
/// <paramref name="DisputeReason"/> is the free text the filer typed - required for every category,
/// not just "Others". It is non-nullable because <c>MarkAsDisputedCommandValidator</c> rejects an
/// empty one before the service is reached, and the notifier leans on that: it is the fallback label
/// when no category was sent.
///
/// <paramref name="EmailInvitationId"/> is not used by the email itself. It identifies the order the
/// acknowledgement belongs to, so the send can be recorded against that order's history - the
/// timeline is per order, and without the id there would be nothing to hang the row on.
/// </remarks>
public record DisputeEmailDetails(
	Guid EmailInvitationId,
	string? RequestorEmail,
	string? RequestorName,
	string CandidateName,
	string? DisputeCategory,
	string DisputeReason);
