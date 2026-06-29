namespace FrontendWebassembly.DTO.ATS;

public record AddressDetailsDTO
{
	public Guid EmailInvitationID { get; set; }
	public string? CurrentCity { get; set; }
	public string? CurrentProvince { get; set; }
	public string? CurrentCountry { get; set; }
	public string? CurrentAddress { get; set; }
	public string? CurrentPostalCode { get; set; }
	public string? TypeOfOwnership { get; set; }
	public string? PermanentAddress { get; set; }
	public string? PermanentCity { get; set; }
	public string? PermanentProvince { get; set; }
	public string? PermanentCountry { get; set; }
	public string? PermanentPostalCode { get; set; }
}
