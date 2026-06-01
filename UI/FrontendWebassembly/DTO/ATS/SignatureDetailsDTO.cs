namespace FrontendWebassembly.DTO.ATS;

public record SignatureDetailsDTO
{
	public byte[]? Signature { get; set; }
	public string? SignerName { get; set; }
	public DateOnly SignatureDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}
