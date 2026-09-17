namespace ATS.Constants;

public static class OrderHistoryEventType
{
	public const string OrderCreated = "OrderCreated";
	public const string ApplicationFormSubmitted = "ApplicationFormSubmitted";
	public const string ApplicationFormWithdrawn = "ApplicationFormWithdrawn";
	public const string ApplicationFormResent = "ApplicationFormResent";

	// A follow-up reminder became due with the form still Pending, so the chaser requeued
	// the invitation. Distinct from ApplicationFormResent because nobody asked for it -
	// reading the history, "a person resent this" and "the schedule did" are different
	// facts. Recorded once per reminder, so an order chased three times carries three.
	public const string ApplicationFormFollowUpSent = "ApplicationFormFollowUpSent";

	// The invitation email actually reached the SMTP server. Every other email event above
	// records that a message was QUEUED; this is the only one that means delivered, which is
	// why "we sent it on the 3rd" can be answered from the history rather than from a log.
	// Written for first invitations, operator resends and follow-up reminders alike - the
	// sender does not distinguish them and neither does this.
	public const string InvitationEmailSent = "InvitationEmailSent";

	public const string ReportUploaded = "ReportUploaded";
	public const string ReportDisputed = "ReportDisputed";

	// The three requestor-facing notices, each distinct from the business event that triggers it
	// for the same reason ApplicationFormFollowUpSent is distinct from ApplicationFormResent:
	// reading the history, "the subject withdrew" and "we told the requestor" are different facts,
	// and the second can fail while the first already happened. All three record the ATTEMPT
	// rather than the delivery, so a row here means the send was made - not that it landed. A
	// failed delivery is in the log, and the row still stands, because the support question
	// "did we try to tell them?" has a different answer from "did they get it?".
	public const string WithdrawalNoticeEmail = "WithdrawalNoticeEmail";
	public const string DisputeAcknowledgementEmail = "DisputeAcknowledgementEmail";
	public const string CompletionNoticeEmail = "CompletionNoticeEmail";

	// A person put an order whose automatic OMS retries were exhausted back on the
	// ticketing queue. The order's own status does not change; this records who did it.
	public const string TicketRetryRequested = "TicketRetryRequested";
}
