namespace ATS.Data.Entities;

public class LicensesDetails
{
	public Guid LicensesDetailsID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? LicenseName { get; set; }
	public string? LicenseNumber { get; set; }
	public DateOnly? LicenseExpiryDate { get; set; }
	public string? LicenseUploadFileKey { get; set; }
	public DateTime? CreatedDate { get; set; }
}