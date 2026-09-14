namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// A new sender mailbox. One-way: nothing here is ever sent back down.
/// </summary>
public class RegisterEmailAccountDTO
{
	public string DisplayName { get; set; } = string.Empty;
	public string EmailAddress { get; set; } = string.Empty;

	// Gmail's defaults, because that is what every account here has been so far. Both stay
	// editable - the server does not assume the provider.
	public string SmtpHost { get; set; } = "smtp.gmail.com";
	public int SmtpPort { get; set; } = 587;

	/// <summary>The provider app password. Held only for the length of the request.</summary>
	public string AppPassword { get; set; } = string.Empty;

	/// <summary>Null lets the server place it after the existing accounts.</summary>
	public int? Priority { get; set; }

	/// <summary>Null takes the configured default, currently 450.</summary>
	public int? DailySendLimit { get; set; }

	public bool IsActive { get; set; } = true;
}
