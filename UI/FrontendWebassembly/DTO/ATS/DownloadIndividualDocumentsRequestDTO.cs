namespace FrontendWebassembly.DTO.ATS;

public class DownloadIndividualDocumentsRequestDTO
{
	public List<DownloadIndividualDocuments> FileDocuments { get; set; } = [];
	public string? SubjectName { get; set; }
}
public class DownloadIndividualDocuments
{
	public string? FileKey { get; set; }
	public string? FileName { get; set; }
}