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
/// This is NOT the internal operations notification. <c>DisputeOrderService</c> also sends
/// <c>IEmailService.SendEmailForDispute</c> to <c>ATS:DisputeOrderEmailRecipient</c> — before the
/// transaction, throwing if it fails. Both messages go out for one dispute. They have different
/// audiences and deliberately different failure semantics: an outage that stops operations from
/// hearing about a dispute should stop the dispute, while an outage that stops the filer getting a
/// courtesy acknowledgement should not.
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
/// <paramref name="DisputeReason"/> is non-nullable because
/// <c>MarkAsDisputedCommandValidator</c> rejects an empty one before the service is reached, and the
/// notifier leans on that: it is the fallback label when no category was sent.
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
