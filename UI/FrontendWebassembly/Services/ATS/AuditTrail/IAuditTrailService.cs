namespace FrontendWebassembly.Services.ATS.AuditTrail;

public interface IAuditTrailService
{
	// A caller who is not a platform super admin reads an empty page rather than an error,
	// which is how every other ATS list behaves.
	Task<ServiceResponse<KeysetPaginatedResult<AuditTrailListDTO>>> GetAuditTrailAsync(
		string? cursor = null,
		int? pageSize = 10,
		string? outcome = null,
		string? action = null,
		string? area = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null);

	Task<ServiceResponse<AuditOutcomeCountsDTO>> GetOutcomeCountsAsync(
		string? action = null,
		string? area = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null);

	/// <summary>
	/// Downloads the filtered trail as a styled Excel workbook. Unlike the reads above, a
	/// caller who is not a platform super admin receives an error rather than an empty
	/// result - a download leaves the system, so it is refused explicitly.
	/// </summary>
	Task<ServiceResponse<HttpResponseMessage>> ExportAuditTrailAsync(
		string? outcome = null,
		string? action = null,
		string? area = null,
		string? searchTerm = null,
		DateTime? startDate = null,
		DateTime? endDate = null,
		CancellationToken cancellationToken = default);
}
