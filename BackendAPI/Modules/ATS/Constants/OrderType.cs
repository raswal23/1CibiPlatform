namespace ATS.Constants;

/// <summary>
/// The turnaround an order is placed at. Public so the endpoint validators and the UI
/// DTO layer can share one vocabulary; before this existed "Rush" and "Normal" were
/// string literals repeated across the AI plugin, the dashboard and the CSV parser,
/// and nothing rejected a third value.
/// </summary>
public static class OrderType
{
	public const string Normal = "Normal";

	public const string Rush = "Rush";

	public static readonly string[] All = [Normal, Rush];

	/// <summary>
	/// Returns the canonical spelling of a caller-supplied order type, or null when it
	/// is not one of the two. Case- and whitespace-insensitive: an integrator writing
	/// "rush" means Rush, and storing it that way keeps every consumer's comparison
	/// working.
	/// </summary>
	public static string? Normalize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var trimmed = value.Trim();

		return All.FirstOrDefault(known =>
			string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase));
	}
}
