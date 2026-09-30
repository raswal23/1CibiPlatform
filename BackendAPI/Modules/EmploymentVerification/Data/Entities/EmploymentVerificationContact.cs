namespace EmploymentVerification.Data.Entities;

/// <summary>
/// A known HR mailbox for a company, held as a directory so staff raising a
/// verification request have a vetted address to reach rather than hunting for one.
/// </summary>
/// <remarks>
/// The same mailbox may legitimately appear under more than one company - a shared
/// HR inbox serving several subsidiaries is normal in the BPO sector this data comes
/// from - so uniqueness is on the (company, email) pair, not on the email alone.
/// See <c>EmploymentVerificationContactConfiguration</c>.
/// </remarks>
public sealed class EmploymentVerificationContact
{
	public Guid Id { get; set; }
	public string CompanyName { get; set; } = "";
	public string EmailAddress { get; set; } = "";

	/// <summary>
	/// Contacts are deactivated, never deleted, matching ATS module and client
	/// management. Inactive rows still appear in the list so a row deactivated by
	/// mistake can be found again.
	/// </summary>
	public bool IsActive { get; set; } = true;

	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
