namespace ATS.Services.EmailService;

/// <summary>
/// Announces a completed application form to the requestor who raised the order.
/// </summary>
/// <remarks>
/// Best-effort by contract, in the same sense as <see cref="IWithdrawnEmailNotification"/> and
/// <see cref="IDisputeEmailNotification"/>: the caller has already committed the submission, so an
/// implementation must never let a delivery failure escape. Callers await it after their commit and
/// do not wrap it themselves.
///
/// For <c>ApplicationFormService.AddApplicationFormDataAsync</c> that is not a nicety. Its
/// <c>catch</c> block deletes every file the submission uploaded as compensation, so an exception
/// escaping after the commit would tear the stored attachments out from under a form that is already
/// saved.
/// </remarks>
public interface ISubmittedFormEmailNotification
{
	/// <summary>
	/// Sends the completed-form notice. Silently does nothing when the order cannot be found or has
	/// no requestor to address.
	/// </summary>
	Task SendAsync(
		SubmittedFormEmailDetails details,
		CancellationToken cancellationToken);
}

/// <summary>
/// What the notice needs from its caller.
/// </summary>
/// <remarks>
/// Only two values, and they come from different places on purpose.
///
/// <paramref name="CandidateName"/> is the name the candidate typed into the form they just
/// submitted, not the one stored on the order when it was raised - this is the first message about
/// the form's contents, so the name they signed it with is the one that matters. It is nullable
/// because a form can be submitted with the name fields blank, and the implementation falls back to
/// the name on the order row, which it loads anyway.
///
/// The form carries no primary email address (only an alternative one), so the candidate's mailbox
/// is read from the order row rather than passed in.
/// </remarks>
public record SubmittedFormEmailDetails(
	Guid EmailInvitationId,
	string? CandidateName);
