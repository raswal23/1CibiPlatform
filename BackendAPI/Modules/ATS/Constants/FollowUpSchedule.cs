namespace ATS.Constants;

/// <summary>
/// The one definition of the timezone the package follow-up schedule runs in.
/// </summary>
/// <remarks>
/// <para>
/// Two places have to agree about what "a day" means: the release query that decides whether a
/// reminder is due (<c>ATSRepository.ReleaseDueFollowUpInvitationsAsync</c>, which passes
/// <see cref="Id"/> to <c>AT TIME ZONE</c>) and the reports list that tells an operator how many
/// reminders are left (<c>ReportService.CalculateFollowUpEmailsRemaining</c>). Two literals would
/// drift, and the symptom would be a board confidently off by one for eight hours a day.
/// </para>
/// <para>
/// Manila rather than UTC is a correctness requirement, not a preference - see
/// <c>docs/ats-package-follow-up-email.md</c>. Because each reminder is anchored to the order's
/// time of day, a UTC-dated dedupe double-sends for orders created between midnight and 8am local.
/// Safe because the Philippines has had no DST since 1978 (fixed UTC+8), so this is plain
/// arithmetic with no ambiguous or skipped local times.
/// </para>
/// </remarks>
public static class FollowUpSchedule
{
	/// <summary>IANA id, used both by Postgres <c>AT TIME ZONE</c> and by <see cref="TimeZone"/>.</summary>
	public const string Id = "Asia/Manila";

	/// <summary>
	/// Resolved once - the lookup reads the system database and is not free per row, and this
	/// runs for every order on every page of the reports board.
	/// </summary>
	/// <remarks>
	/// The IANA id resolves on Linux natively and on Windows through the ICU data .NET ships
	/// with, so this is safe on both without a Windows/IANA fallback.
	/// </remarks>
	public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById(Id);
}
