namespace ATS.Services.Report;

public interface IReportService
{
	Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO, CancellationToken cancellationToken = default);
	Task<KeysetPaginatedResult<ReportListDTO>> GetReportsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	/// <summary>
	/// Corrects the subject name on one order. The caller must be able to see that
	/// order under their ATS access scope, otherwise this throws.
	/// </summary>
	Task<SubjectNameDTO> EditSubjectNameAsync(EditSubjectNameDTO subjectName, CancellationToken cancellationToken);
	Task<ReportResultDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
	/// <summary>
	/// Zips the requested documents for one order. The subject name comes back with the
	/// stream so the endpoint can name the file without trusting caller input.
	/// </summary>
	Task<(Stream ZipStream, string SubjectName)> DownloadIndividualReportAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken);
	Task<Stream> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken);
}
