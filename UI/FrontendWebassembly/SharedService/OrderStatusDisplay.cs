namespace FrontendWebassembly.SharedService;

public static class OrderStatusDisplay
{
	// Mirrors BackendAPI/Modules/ATS/Constants/OrderStatus.cs
	public const string Completed = "Completed";
	public const string InProgress = "In Progress";
	public const string PendingCandidateInfo = "Pending Candidate Info";
	public const string ApplicationWithdrawn = "Application Withdrawn";

	public static string GetText(string? status)
	{
		if (string.IsNullOrWhiteSpace(status)) return "Unknown";
		if (Is(status, PendingCandidateInfo)) return "Pending";
		if (Is(status, ApplicationWithdrawn)) return "Withdrawn";
		return status.Trim();
	}

	public static string GetClass(string? status)
	{
		if (Is(status, Completed)) return "completed";
		if (Is(status, InProgress)) return "progress";
		if (Is(status, PendingCandidateInfo)) return "pending";
		if (Is(status, ApplicationWithdrawn)) return "withdrawn";
		return "unknown";
	}

	private static bool Is(string? a, string b)
		=> string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
