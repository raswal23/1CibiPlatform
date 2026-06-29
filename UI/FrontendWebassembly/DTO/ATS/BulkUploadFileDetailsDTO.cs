namespace FrontendWebassembly.DTO.ATS;

public record BulkUploadFileDetailsDTO
{
	public string? FileName { get; set; }
	public string? Status { get; set; }
	public string? PackageType { get; set; }
	public string? OrderType { get; set; }
	public IBrowserFile? BulkFile { get; set; }
}
