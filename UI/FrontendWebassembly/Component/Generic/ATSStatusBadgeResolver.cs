namespace FrontendWebassembly.Component.Generic;

public static class ATSStatusBadgeResolver
{
	public static string GetBadgeClass(string? status)
	{
		if (string.IsNullOrWhiteSpace(status))
			return "ats-status-badge ats-status-neutral";

		var normalized = status.Trim().ToLowerInvariant();

		if (normalized.Contains("complete") || normalized.Contains("success") || normalized == "clear" || normalized.Contains("approved"))
			return "ats-status-badge ats-status-success";

		if (normalized.Contains("in progress") || normalized.Contains("processing") || normalized.Contains("on going") || normalized.Contains("ongoing") || normalized.Contains("review"))
			return "ats-status-badge ats-status-warning";

		if (normalized.Contains("pending"))
			return "ats-status-badge ats-status-pending";

		if (normalized.Contains("failed") || normalized.Contains("reject") || normalized.Contains("cancel") || normalized.Contains("error") || normalized.Contains("dispute") || normalized.Contains("withdrawn") || normalized.Contains("expired"))
			return "ats-status-badge ats-status-error";

		return "ats-status-badge ats-status-neutral";
	}
}
