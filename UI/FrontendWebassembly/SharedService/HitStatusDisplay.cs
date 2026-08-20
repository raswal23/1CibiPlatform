namespace FrontendWebassembly.SharedService;

public static class HitStatusDisplay
{
	public const string Clear = "Clear";
	public const string NotClear = "Not Clear";

	private static bool Is(string? a, string b)
		=> string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

	private static bool IsEmpty(string? status)
		=> string.IsNullOrWhiteSpace(status) || status.Trim() == "-";

	public static string GetText(string? status)
	{
		if (IsEmpty(status)) return "Pending";
		if (Is(status, Clear)) return "Clear";
		if (Is(status, NotClear) || Is(status, "NotClear")) return "Not clear";
		return status!.Trim();
	}

	public static string GetClass(string? status)
	{
		if (IsEmpty(status)) return "pending";
		if (Is(status, Clear)) return "clear";
		if (Is(status, NotClear) || Is(status, "NotClear")) return "not-clear";
		return "unknown";
	}
}
