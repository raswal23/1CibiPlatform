using ATS.Services.EmailService;
using Test.BackendAPI.Infrastructure.Auth.Infrastructure;

namespace Test.BackendAPI.Infrastructure.ATS.Infrastracture;

/// <summary>
/// The ATS integration host's stand-in for the keyed <c>"ats"</c> sender. Adds the ATS-only
/// contract to the shared <see cref="FakeEmailSender"/>; still sends nothing.
/// </summary>
/// <remarks>
/// <c>ATSServiceConfiguration</c> registers <c>IAtsEmailSender</c> by casting whatever the keyed
/// <c>"ats"</c> <c>IEmailService</c> resolves to:
///
/// <code>
/// services.AddScoped&lt;IAtsEmailSender&gt;(provider =>
///     (IAtsEmailSender)provider.GetRequiredKeyedService&lt;IEmailService&gt;("ats"));
/// </code>
///
/// Substituting the plain <see cref="FakeEmailSender"/> here makes that cast throw — and it throws
/// while RESOLVING, not while sending, so it takes down every test whose dependency graph reaches a
/// service that injects <c>IAtsEmailSender</c> directly, however unrelated to email the test is.
/// Deriving and adding the ATS members keeps the cast honest while still putting no message on the
/// wire.
///
/// <c>EndorsementSubmissionService</c> guards the same cast with <c>as</c> and degrades to the
/// first-invitation body when it fails; a service that depends on <c>IAtsEmailSender</c> directly
/// has no such fallback, which is why the fake has to satisfy it.
/// </remarks>
public sealed class FakeAtsEmailSender : FakeEmailSender, IAtsEmailSender
{
	public Task<EmailDeliveryResult> SendATSEmailWithResultAsync(
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken,
		IReadOnlyCollection<string>? cc = null)
		=> Task.FromResult(EmailDeliveryResult.Sent);

	public Task<EmailDeliveryResult> SendThroughAccountAsync(
		int accountId,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken,
		IReadOnlyCollection<string>? cc = null)
		=> Task.FromResult(EmailDeliveryResult.Sent);

	public Task<EmailDeliveryResult> SendWithCredentialsAsync(
		SmtpAccountCredentials credentials,
		string toEmail,
		string subject,
		string body,
		CancellationToken cancellationToken)
		=> Task.FromResult(EmailDeliveryResult.Sent);

	public string BuildApplicationFormReminderNotification(
		string gmail,
		string name,
		string applicationFormLink,
		string? requestor,
		string? clientName)
		=> $"Reminder for {name}/{gmail}: we have not yet received your application form. Use this link: {applicationFormLink}";

	public string BuildWithdrawnApplicationNotification(
		string requestorName,
		string candidateName)
		=> $"Dear {requestorName}, your candidate {candidateName} has withdrawn their Application Form.";

	public string BuildDisputeNotification(
		string requestorName,
		string candidateName,
		string disputeCategory,
		string? disputeDetails)
		=> $"Dear {requestorName}, a dispute has been submitted for {candidateName}. Category: {disputeCategory}. Details: {disputeDetails ?? "(none)"}";

	public string BuildSubmittedFormNotification(
		string requestorName,
		string candidateName)
		=> $"Dear {requestorName}, your candidate {candidateName} has successfully completed the Application Form.";
}
