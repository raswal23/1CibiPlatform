namespace FrontendWebassembly.Component.ATS;

public static class ATSDashboardChartKeys
{
	public const string UserYtdHire = "user-ytd-hire";
	public const string CandidateResponseRate = "candidate-response-rate";
	public const string TurnaroundTimeTrend = "turnaround-time-trend";
	public const string CompletionRate = "completion-rate";

	private static readonly IReadOnlyDictionary<string, string> Titles =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[UserYtdHire] = "User YTD Hire",
			[CandidateResponseRate] = "Candidate Response Rate",
			[TurnaroundTimeTrend] = "Turnaround Time Trend",
			[CompletionRate] = "Completion Rate"
		};

	public static bool TryGetTitle(string? chartKey, out string title)
	{
		if (!string.IsNullOrWhiteSpace(chartKey)
			&& Titles.TryGetValue(chartKey.Trim(), out var chartTitle))
		{
			title = chartTitle;
			return true;
		}

		title = string.Empty;
		return false;
	}
}
