namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// An edit to a registered sender mailbox.
/// </summary>
/// <remarks>
/// A null or empty <see cref="AppPassword"/> means "keep the stored one" - the existing password
/// is never sent to the browser, so there is nothing to post back unchanged. Changing the email,
/// password, host or port triggers a fresh code; the rest saves immediately.
/// </remarks>
public class EditEmailAccountDTO
{
	public int AtsEmailAccountId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public string EmailAddress { get; set; } = string.Empty;
	public string SmtpHost { get; set; } = string.Empty;
	public int SmtpPort { get; set; }
	public string? AppPassword { get; set; }
	public int Priority { get; set; }
	public int DailySendLimit { get; set; }
	public bool IsActive { get; set; }
}
