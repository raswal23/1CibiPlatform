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
	/// Label shown for a stored <c>Rejected</c> status. The employer did respond, and
	/// what they said is that the details are wrong - "Rejected" reads as though CIBI
	/// turned the request down, which is the opposite of what happened. Shared because
	/// the tracking grid's status chip, its CSS mapping and its stat tile all have to
	/// spell it identically.
	/// </summary>
	public const string InaccurateStatusLabel = "Verified with inaccuracy";

	/// <summary>
	/// A sent request whose link has lapsed is shown as expired: the backend
	/// releases the candidate for a new request at that point, but the row itself
	/// keeps its stored status.
	/// </summary>
	/// <remarks>
	/// Both mappings here are display only. The stored enum stays <c>Sent</c> and
	/// <c>Rejected</c>, which is what the availability rule, the response-rate count
	/// and the single-use guard all read - relabelling in the database would change
	/// behaviour, not wording.
	/// </remarks>
	public static string GetDisplayStatus(SentVerificationRequestDTO request)
	{
		if (request.Status == "Sent" && request.TokenExpiresAt < DateTime.UtcNow)
		{
			return "Expired";
		}

		return request.Status == "Rejected"
			? InaccurateStatusLabel
			: request.Status;
	}

	/// <summary>
	/// The reason the HR contact gave, or an em dash when there is none. Only a
	/// rejected row carries one, so every other outcome shows the dash rather than an
	/// empty cell that reads as a value failing to load.
	/// </summary>
	public static string GetInaccuracyReason(SentVerificationRequestDTO request) =>
		string.IsNullOrWhiteSpace(request.ResponseNotes)
			? "—"
			: request.ResponseNotes;

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
