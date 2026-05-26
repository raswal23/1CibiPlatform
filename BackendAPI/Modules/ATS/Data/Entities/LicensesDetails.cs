namespace ATS.Data.Entities;

public class LicensesDetails
{
	public Guid LicensesDetailsID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? LicenseName { get; set; }
	public string? LicenseNumber { get; set; }
	public string? LicenseExpiryDate { get; set; }
	public byte[]? LicenseUpload { get; set; }
	public DateTime? CreatedDate { get; set; }
}