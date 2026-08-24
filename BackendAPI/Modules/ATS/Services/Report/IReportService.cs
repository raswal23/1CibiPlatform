namespace ATS.Services.Report;

public interface IReportService
{
	Task<bool> UploadReportAsync(ReportDetailsDTO reportDetailsDTO, CancellationToken cancellationToken = default);
	Task<KeysetPaginatedResult<ReportListDTO>> GetReportsAsync(KeysetPaginationRequest paginationRequest, CancellationToken cancellationToken);
	Task<ReportResultDTO> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
	/// <summary>
	/// Zips the requested documents for one order. The subject name comes back with the
	/// stream so the endpoint can name the file without trusting caller input.
	/// </summary>
	Task<(Stream ZipStream, string SubjectName)> DownloadIndividualReportAsync(DownloadIndividualDocumentsRequestDTO downloadInvididualRequest, CancellationToken cancellationToken);
	Task<Stream> DownloadMultipleOrderRecordsAsync(DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest, CancellationToken cancellationToken);
}
