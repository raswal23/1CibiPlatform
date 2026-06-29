namespace ATS.Data.DTO;

public record BulkUploadFileDetailsDTO
{
	public Guid UploadedByUserId { get; set; }
	public string? FileName { get; set; }
	public string? Status { get; set; }
	public string? PackageType { get; set; }
	public string? OrderType { get; set; }
	public DateTime DateCreated { get; set; }
	public IFormFile? BulkFile { get; set; }
}

public class BulkUploadCsvRecord
{
	public string? LastName { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleInitial { get; set; }
	public string? EmailAddress { get; set; }
	public string? MobileNumber { get; set; }
}