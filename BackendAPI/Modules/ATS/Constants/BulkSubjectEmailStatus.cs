namespace ATS.Constants;

// The public, caller-facing vocabulary for filtering a bulk file's subjects by the
// progress of their invitation email. Deliberately separate from the internal
// EmailStatus constants the background job writes: Pending and Processing are both
// "still in flight" to a requestor, so the dashboard collapses them into one bucket
// rather than exposing the job's internal claim state.
public static class BulkSubjectEmailStatus
{
	// Matches EmailStatus.Pending or EmailStatus.Processing.
	public const string Pending = "Pending";

	// Matches EmailStatus.Done.
	public const string Sent = "Sent";

	// Matches EmailStatus.Error.
	public const string Failed = "Failed";

	// The full vocabulary, used to validate a caller-supplied status filter.
	public static readonly string[] All = [Pending, Sent, Failed];
}
