namespace FrontendWebassembly.DTO.ATS;

public record LicensesDetailsDTO
{
	public Guid EmailInvitationID { get; set; }
	public string? LicenseName { get; set; }
	public string? LicenseNumber { get; set; }
	public DateOnly? LicenseExpiryDate { get; set; }
	public byte[]? LicenseUploadFile { get; set; }
	public string? LicenseUploadFileName { get; set; }
}
