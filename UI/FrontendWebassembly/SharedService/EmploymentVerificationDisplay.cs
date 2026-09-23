namespace FrontendWebassembly.SharedService;

/// <summary>
/// Formatting shared by the Employment Verification console pages. These helpers
/// used to sit on the single combined page; they live here now because the Needs
/// request and Tracking views became separate routes and both still need them, and
/// a second copy would be the thing that drifts.
/// </summary>
public static class EmploymentVerificationDisplay
{
	/// <summary>
	/// Employment period as "Mon yyyy – Mon yyyy". An absent end date reads as
	/// "Present" rather than as unknown, because an in-progress record legitimately
	/// has no end.
	/// </summary>
	public static string GetEmploymentPeriod(DateOnly? startDate, DateOnly? endDate)
	{
		if (startDate is null && endDate is null)
		{
			return "Not provided";
		}

		var start = startDate?.ToString("MMM yyyy") ?? "Unknown";
		var end = endDate?.ToString("MMM yyyy") ?? "Present";

		return $"{start} – {end}";
	}

	/// <summary>
	/// A sent request whose link has lapsed is shown as expired: the backend
	/// releases the candidate for a new request at that point, but the row itself
	/// keeps its stored status.
	/// </summary>
	public static string GetDisplayStatus(SentVerificationRequestDTO request) =>
		request.Status == "Sent" && request.TokenExpiresAt < DateTime.UtcNow
			? "Expired"
			: request.Status;

	public static string GetRespondedOn(SentVerificationRequestDTO request)
	{
		var respondedAt = request.VerifiedAt ?? request.RejectedAt;

		return respondedAt?.ToLocalTime().ToString("MMM dd, yyyy") ?? "—";
	}

	/// <summary>
	/// Share of sent requests that came back confirmed. Reported as an em dash
	/// until something has actually been sent, rather than as 0%.
	/// </summary>
	public static string GetResponseRate(IReadOnlyCollection<SentVerificationRequestDTO> requests)
	{
		if (requests.Count == 0)
		{
			return "—";
		}

		var answered = requests.Count(request =>
			request.Status is "Verified" or "Rejected");

		return $"{answered * 100 / requests.Count}%";
	}

	public static DateTime? ToDateTime(DateOnly? value) =>
		value?.ToDateTime(TimeOnly.MinValue);

	public static bool Matches(string? value, string term) =>
		!string.IsNullOrWhiteSpace(value) &&
		value.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase);
}
