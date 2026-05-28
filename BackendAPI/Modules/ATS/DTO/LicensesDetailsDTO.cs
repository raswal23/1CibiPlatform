
public record LicensesDetailsDTO
{
	public Guid LicensesDetailsID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? LicenseName { get; set; }
	public string? LicenseNumber { get; set; }
	public DateTime? LicenseExpiryDate { get; set; }
	public byte[]? LicenseUploadFile { get; set; }
	public string? LicenseUploadFileName { get; set; }
	public DateTime? CreatedDate { get; set; }
}
