namespace ATS.Constants;

public static class OrderHistoryEventType
{
	public const string OrderCreated = "OrderCreated";
	public const string ApplicationFormSubmitted = "ApplicationFormSubmitted";
	public const string ApplicationFormWithdrawn = "ApplicationFormWithdrawn";
	public const string ApplicationFormResent = "ApplicationFormResent";
	public const string ReportUploaded = "ReportUploaded";
	public const string ReportDisputed = "ReportDisputed";

	// A person put an order whose automatic OMS retries were exhausted back on the
	// ticketing queue. The order's own status does not change; this records who did it.
	public const string TicketRetryRequested = "TicketRetryRequested";
}
