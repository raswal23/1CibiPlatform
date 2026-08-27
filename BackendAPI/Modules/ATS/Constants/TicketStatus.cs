namespace ATS.Constants;

/// <summary>
/// Lifecycle of an order's OMS ticket. Public, unlike <see cref="EmailStatus"/>,
/// because the ticketing status screen filters on this vocabulary.
/// </summary>
public static class TicketStatus
{
	public const string Pending = "Pending";

	public const string Processing = "Processing";

	public const string Done = "Done";

	// Terminal until a human intervenes: either OMS rejected the request on business
	// grounds, or the order could not be projected onto the OMS payload at all.
	public const string Error = "Error";

	// The full vocabulary, used to validate a caller-supplied status filter.
	public static readonly string[] All = [Pending, Processing, Done, Error];
}
