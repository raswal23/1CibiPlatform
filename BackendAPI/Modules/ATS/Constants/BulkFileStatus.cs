namespace ATS.Constants;

public static class BulkFileStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";

	// The full vocabulary, used to validate a caller-supplied status filter.
	public static readonly string[] All = [Pending, Processing, Done];
}
