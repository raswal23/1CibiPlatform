namespace FrontendWebassembly.DTO.ATS;

public record BulkUploadFileDetailsDTO
{
	public Guid FileID { get; set; }
	public string? FileName { get; set; }
	public string? Status { get; set; }
	public DateTime DateCreated { get; set; }
	public byte[]? BulkFile { get; set; }
}
