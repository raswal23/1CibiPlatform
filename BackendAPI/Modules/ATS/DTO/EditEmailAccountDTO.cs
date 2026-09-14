namespace ATS.DTO;

/// <summary>
/// An edit to a registered sender account.
/// </summary>
/// <remarks>
/// <see cref="AppPassword"/> is null when the operator did not retype it, which is the normal
/// case: a UI that round-tripped the existing password to let the field be "pre-filled" would
/// have to send it to the browser, and no DTO here carries it in any form.
///
/// Four fields can invalidate the proof a previous code gave - email, password, host, port -
/// and changing any of them re-runs verification. The rest save immediately.
/// </remarks>
public class EditEmailAccountDTO
{
	public int AtsEmailAccountId { get; set; }

	public string DisplayName { get; set; } = string.Empty;

	public string EmailAddress { get; set; } = string.Empty;

	public string SmtpHost { get; set; } = string.Empty;

	public int SmtpPort { get; set; }

	/// <summary>Null or blank means "keep the stored password".</summary>
	public string? AppPassword { get; set; }

	public int Priority { get; set; }

	public int DailySendLimit { get; set; }

	public bool IsActive { get; set; }
}
