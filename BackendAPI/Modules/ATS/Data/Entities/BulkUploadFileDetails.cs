namespace ATS.Data.Entities;

public class BulkUploadFileDetails
{
	public Guid FileID { get; set; }
	public string? FileName { get; set; }
	public string? Status { get; set; }
	public DateTime DateCreated { get; set; }
}
