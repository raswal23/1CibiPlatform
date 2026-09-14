namespace ATS.DTO;

/// <summary>
/// A new sender account, before its credentials have been proven.
/// </summary>
/// <remarks>
/// One-way: this type travels browser to server only. The password is the app password for the
/// mailbox, and the row it creates is written as Pending - invisible to the selector - until a
/// code sent through these exact credentials comes back verified.
/// </remarks>
public class RegisterEmailAccountDTO
{
	public string DisplayName { get; set; } = string.Empty;

	public string EmailAddress { get; set; } = string.Empty;

	public string SmtpHost { get; set; } = "smtp.gmail.com";

	public int SmtpPort { get; set; } = 587;

	public string AppPassword { get; set; } = string.Empty;

	/// <summary>Null means "after the existing accounts".</summary>
	public int? Priority { get; set; }

	public int? DailySendLimit { get; set; }

	public bool IsActive { get; set; } = true;
}
