namespace ATS.Services.AuditTrail;

public interface IAtsAuditService
{
	Task<KeysetPaginatedResult<AuditTrailListDTO>> GetAuditTrailAsync(
		KeysetPaginationRequest paginationRequest,
		string? outcome,
		string? action,
		string? area,
		CancellationToken cancellationToken);

	Task<AuditOutcomeCountsDTO> GetOutcomeCountsAsync(
		string? action,
		string? area,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken);

	/// <summary>
	/// The newest entries matching the filters, projected to the narrow shape the AI
	/// assistant reports. Never paginates - it always reads the first page.
	///
	/// Returns an empty list for a caller who is not a platform super admin, exactly as the
	/// paged read does. That matters more here than elsewhere: the assistant is available to
	/// every ATS role, so this is the boundary that keeps the audit trail admin-only.
	/// </summary>
	Task<IReadOnlyList<AtsAuditEntrySummaryDTO>> GetRecentEntriesAsync(
		string? outcome,
		string? action,
		string? area,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		int take,
		CancellationToken cancellationToken);

	/// <summary>
	/// Renders the filtered trail as a styled .xlsx workbook, failures highlighted and the
	/// cause of each in its own column.
	///
	/// Unlike the read methods this THROWS <c>ForbiddenException</c> for a caller who may
	/// not read the trail, rather than returning an empty result: a download leaves the
	/// system, and handing someone a plausible-looking empty file is worse than telling
	/// them no.
	/// </summary>
	Task<AtsAuditExportDTO> ExportAuditTrailAsync(
		string? outcome,
		string? action,
		string? area,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		CancellationToken cancellationToken);
}
