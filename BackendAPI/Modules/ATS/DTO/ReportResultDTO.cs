namespace ATS.DTO;

public record ReportResultDTO
{
	public string? SubjectName { get; set; }
	public string? OrderStatus { get; set; }
	public string? HitStatus { get; set; }
	public string? SelectedPackage { get; set; }
	public string? ResumeFileName { get; set; }
	public string? IdUploadedFileName { get; set; }
	public string? CoeFileName { get; set; }
	public string? DiplomaFileName { get; set; }
	public string? BiometricPhotoFileName { get; set; }
	public string? ConsentFormFileName { get; set; }
	public string? UploadedReportFileName { get; set; }
	public DateTime? ReportUploadedAt { get; set; }
}
