namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public async Task<ReportDetails?> GetReportDetailsByStatusAsync(Guid emailInvitationRequestId, string reportStatus, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetReportDetailsByStatusAsync(emailInvitationRequestId, reportStatus, cancellationToken);
	}

	public async Task<bool> AddReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.AddReportDetailsAsync(reportDetails, cancellationToken);
		if (result)
		{
			await _hybridCache.RemoveByTagAsync(CacheTags.Report);
			await _hybridCache.RemoveByTagAsync(CacheTags.DisputeOrder);
		}

		return result;
	}

	public async Task<bool> UpdateReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.UpdateReportDetailsAsync(reportDetails, cancellationToken);
		if (result)
		{
			await _hybridCache.RemoveByTagAsync(CacheTags.Report);
			await _hybridCache.RemoveByTagAsync(CacheTags.DisputeOrder);
		}
		return result;
	}

	public async Task<bool> UpdateOrderStatusAsync(Guid EmailInvitationRequestId, string orderStatus, DateTime? orderCompletedAt, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.UpdateOrderStatusAsync(EmailInvitationRequestId, orderStatus, orderCompletedAt, cancellationToken);

		if (result)
		{
			await _hybridCache.RemoveByTagAsync(CacheTags.DisputeOrder);
			await _hybridCache.RemoveByTagAsync(CacheTags.Report);
		}

		return result;
	}

	public async Task<bool> AddArchiveReportAsync(ArchiveReport archiveReport, CancellationToken cancellationToken)
	{
		return await _atsRepository.AddArchiveReportAsync(archiveReport, cancellationToken);
	}

	public async Task<List<ReportRowDTO>> GetReportsPageAsync(
		int? afterRank,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		if (afterId.HasValue)
			return await _atsRepository.GetReportsPageAsync(afterRank, afterCompletedAt, afterId, take, authorizedClientIds, requiredRequestorId, cancellationToken);

		var cacheKey = $"report_first_take_{take}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<List<ReportRowDTO>>(
			cacheKey,
			async token => await _atsRepository.GetReportsPageAsync(null, null, null, take, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.Report],
			cancellationToken: cancellationToken);
	}

	public async Task<long> CountReportsAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"report_count_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<long>(
			cacheKey,
			async token => await _atsRepository.CountReportsAsync(authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.Report],
			cancellationToken: cancellationToken);
	}

	public async Task<ReportResultDTO?> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		var cacheKey = $"report_result_{emailInvitationRequestId}";

		return await _hybridCache.GetOrCreateAsync(
			cacheKey,
			async token => await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(emailInvitationRequestId, token),
			options: new HybridCacheEntryOptions
			{
				Expiration = TimeSpan.FromMinutes(5)
			},
			tags: [CacheTags.Report],
			cancellationToken: cancellationToken);
	}

	public async Task<List<ReportRowDTO>> SearchReportsPageAsync(
		int? afterRank,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		if (afterId.HasValue)
			return await _atsRepository.SearchReportsPageAsync(afterRank, afterCompletedAt, afterId, take, searchTerm, startDate, endDate, authorizedClientIds, requiredRequestorId, cancellationToken);

		var cacheKey =
			$"report_first" +
			$"_take_{take}" +
			$"_search_{searchTerm ?? "none"}" +
			$"_start_{(startDate.HasValue ? startDate.Value.ToString("yyyyMMdd") : "none")}" +
			$"_end_{(endDate.HasValue ? endDate.Value.ToString("yyyyMMdd") : "none")}" +
			$"_clients_{ClientScope(authorizedClientIds)}" +
			$"_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<List<ReportRowDTO>>(
			cacheKey,
			async token => await _atsRepository.SearchReportsPageAsync(null, null, null, take, searchTerm, startDate, endDate, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.Report],
			cancellationToken: cancellationToken);
	}

	public async Task<long> CountSearchReportsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var cacheKey =
			$"report_count" +
			$"_search_{searchTerm ?? "none"}" +
			$"_start_{(startDate.HasValue ? startDate.Value.ToString("yyyyMMdd") : "none")}" +
			$"_end_{(endDate.HasValue ? endDate.Value.ToString("yyyyMMdd") : "none")}" +
			$"_clients_{ClientScope(authorizedClientIds)}" +
			$"_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<long>(
			cacheKey,
			async token => await _atsRepository.CountSearchReportsAsync(searchTerm, startDate, endDate, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.Report],
			cancellationToken: cancellationToken);
	}

	public async Task<List<DownloadDocumentDTO>> GetDownloadDocumentsAsync(List<Guid> emailInvitationRequestIds, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetDownloadDocumentsAsync(emailInvitationRequestIds, cancellationToken);
	}
}
