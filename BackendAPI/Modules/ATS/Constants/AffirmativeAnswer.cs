namespace ATS.Constants;

/// <summary>
/// Reads the application form's yes/no answers back out of the database.
/// </summary>
/// <remarks>
/// The form binds these to a checkbox (<c>bool</c>), the DTO carries <c>bool?</c>, and
/// the entity column is <c>string?</c> - Mapster bridges the last hop, so what is
/// actually stored is <c>"True"</c>/<c>"False"</c>. Older rows and hand-edited data use
/// <c>"Yes"</c>/<c>"No"</c>, hence both spellings.
/// <para>
/// It fails closed on purpose: null, blank and anything unrecognised all read as "no".
/// For <c>PermissionToContact</c> that is the difference between a candidate who agreed
/// their employer may be contacted and one who simply never answered, and only the
/// first is consent.
/// </para>
/// </remarks>
internal static class AffirmativeAnswer
{
	internal static bool IsAffirmative(string? value)
	{
		var trimmed = value?.Trim();

		return string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase);
	}
}
