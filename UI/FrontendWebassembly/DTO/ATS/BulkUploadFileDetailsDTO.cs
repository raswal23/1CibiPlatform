namespace FrontendWebassembly.DTO.ATS;

public record BulkUploadFileDetailsDTO
{
	public string? FileName { get; set; }
	public string? Status { get; set; }
	public string? PackageType { get; set; }
	public string? OrderType { get; set; }

	// Screening type (true = manual, false = data); gates the package list, and
	// data files must carry the identity columns in every row.
	public bool? AutoChasing { get; set; }

	public IBrowserFile? BulkFile { get; set; }
}
