namespace ATS.Data.Cache;

public class ATSCacheRepository : IATSRepository
{
	private readonly IATSRepository _atsRepository;
	private readonly HybridCache _hybridCache;

	public ATSCacheRepository(IATSRepository atsRepository, HybridCache hybridCache)
	{
		_atsRepository = atsRepository;
		_hybridCache = hybridCache;
	}
	public async Task<bool> AddAddressDetailsAsync(AddressDetails addressDetails)
	{
		return await _atsRepository.AddAddressDetailsAsync(addressDetails);
	}

	public async Task<bool> AddEducationalBackgroundAsync(EducationalBackground educationalBackground)
	{
		return await _atsRepository.AddEducationalBackgroundAsync(educationalBackground);
	}

	public async Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest)
	{
		var result = await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Report);

		return result;
	}

	public async Task<bool> AddLicensesDetailsAsync(LicensesDetails licensesDetails)
	{
		return await _atsRepository.AddLicensesDetailsAsync(licensesDetails);
	}

	public async Task<bool> AddPersonalDetailsAsync(PersonalDetails personalDetails)
	{
		await _hybridCache.RemoveByTagAsync(CacheTags.Report);
		return await _atsRepository.AddPersonalDetailsAsync(personalDetails);
	}

	public async Task<bool> AddProfessionalExperiencesAsync(ProfessionalExperiences professionalExperiences)
	{
		return await _atsRepository.AddProfessionalExperiencesAsync(professionalExperiences);
	}

	public async Task<bool> AddReferenceDetailsAsync(ReferenceDetails referenceDetails)
	{
		return await _atsRepository.AddReferenceDetailsAsync(referenceDetails);
	}

	public async Task<bool> AddSignatureDetailsAsync(SignatureDetails signatureDetails)
	{
		return await _atsRepository.AddSignatureDetailsAsync(signatureDetails);
	}

	public async Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string hashToken, CancellationToken cancellationToken)
	{
		var cacheKey = $"ATS_ApplicationFormStatus_{hashToken}";

		return await _hybridCache.GetOrCreateAsync<EmailIdAndApplicationFormPathDTO>(
			cacheKey,
			async id => await _atsRepository.GetEmailIdAndApplicationFormPathAsync(hashToken, cancellationToken),
			null,
			tags: [CacheTags.WithdrawnApplication]);
	}

	public async Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails)
	{
		return await _atsRepository.AddBulkUploadFileDetailsAsync(bulkUploadFileDetails);
	}

	public async Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync()
	{
		return await _atsRepository.GetBulkUploadFileDetailsAsync();
	}

	public async Task<int> ReleaseBulkFileClaimsAsync(List<BulkUploadFileDetails> bulkUploadFileDetails)
	{
		return await _atsRepository.ReleaseBulkFileClaimsAsync(bulkUploadFileDetails);
	}

	public async Task<int> ReleaseStaleBulkFileClaimsAsync(TimeSpan staleAfter)
	{
		return await _atsRepository.ReleaseStaleBulkFileClaimsAsync(staleAfter);
	}

	public async Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync()
	{
		return await _atsRepository.GetPendingEmailInvitationRequestsAsync();
	}

	public async Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter)
	{
		return await _atsRepository.ReleaseStaleEmailInvitationClaimsAsync(staleAfter);
	}

	public async Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		var result = await _atsRepository.AddBulkEmailInvitationRequestAsync(emailInvitationRequests);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Report);

		return result;
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		return await _atsRepository.UpdateBulkEmailInvitationRequestForSentEmailAsync(emailInvitationRequests);
	}

	public async Task<bool> UpdateBulkFileDetailsStatusAsync(List<Guid> bulkUploadFileDetailIds, string orderStatus)
	{
		return await _atsRepository.UpdateBulkFileDetailsStatusAsync(bulkUploadFileDetailIds, orderStatus);
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId)
	{
		return await _atsRepository.UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(emailInvitationId);
	}

	public async Task<bool> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken)
	{
		var cacheKey = $"ATS_ApplicationFormStatus_{hashToken}";

		return await _hybridCache.GetOrCreateAsync(
			cacheKey,
			async id => await _atsRepository.IsHashTokenValidAsync(hashToken, cancellationToken));
	}

	public async Task<bool> UpdateEmailInvitationRequestForFilledUpFormAsync(Guid emailInvitationRequestId)
	{
		var result = await _atsRepository.UpdateEmailInvitationRequestForFilledUpFormAsync(emailInvitationRequestId);

		await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication);
		await _hybridCache.RemoveByTagAsync(CacheTags.Report);

		return result;
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		return await _atsRepository.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(emailInvitationRequests);
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId)
	{
		return await _atsRepository.UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(emailInvitationId);
	}

	public async Task<int> WithdrawnApplicationForm(string hashToken, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.WithdrawnApplicationForm(hashToken, cancellationToken); 
		await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication);
		return result;
	}

	// Keyset pagination caches only the first page (null seek anchor); cursor pages
	// are high-cardinality and go straight to the repository.
	public async Task<List<EmailInvitationRequestListDTO>> GetWithdrawnPageAsync(
		string? searchTerm,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		if (afterId.HasValue)
			return await _atsRepository.GetWithdrawnPageAsync(searchTerm, afterId, take, authorizedClientIds, requiredRequestorId, cancellationToken);

		var cacheKey = $"withdrawnapplication_first_take_{take}_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<List<EmailInvitationRequestListDTO>>(
			cacheKey,
			async token => await _atsRepository.GetWithdrawnPageAsync(searchTerm, null, take, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.WithdrawnApplication],
			cancellationToken: cancellationToken);
	}

	public async Task<long> CountWithdrawnAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"withdrawnapplication_count_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<long>(
			cacheKey,
			async token => await _atsRepository.CountWithdrawnAsync(searchTerm, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.WithdrawnApplication],
			cancellationToken: cancellationToken);
	}

	private static string ClientScope(IReadOnlyCollection<int>? authorizedClientIds) =>
		authorizedClientIds is null
			? "all"
			: string.Join('-', authorizedClientIds.OrderBy(clientId => clientId));

	private static string RequestorScope(Guid? requiredRequestorId) =>
		requiredRequestorId?.ToString("N") ?? "all";

	public async Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetEmailInvitationRequestByIdAsync(emailInvitationId, cancellationToken);
	}

	public async Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.ResendApplicationFormAsync(emailInvitationId, hashToken, hashTokenExpiration, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication);

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

	public async Task<List<DisputeOrderListDTO>> GetDisputeOrdersPageAsync(
		string? searchTerm,
		bool? afterHasDispute,
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		if (afterId.HasValue)
			return await _atsRepository.GetDisputeOrdersPageAsync(searchTerm, afterHasDispute, afterCreatedAt, afterId, take, authorizedClientIds, requiredRequestorId, cancellationToken);

		var cacheKey = $"disputeorder_first_take_{take}_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<List<DisputeOrderListDTO>>(
			cacheKey,
			async token => await _atsRepository.GetDisputeOrdersPageAsync(searchTerm, null, null, null, take, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.DisputeOrder],
			cancellationToken: cancellationToken);
	}

	public async Task<long> CountDisputeOrdersAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"disputeorder_count_search_{searchTerm}_clients_{ClientScope(authorizedClientIds)}_requestor_{RequestorScope(requiredRequestorId)}";

		return await _hybridCache.GetOrCreateAsync<long>(
			cacheKey,
			async token => await _atsRepository.CountDisputeOrdersAsync(searchTerm, authorizedClientIds, requiredRequestorId, token),
			tags: [CacheTags.DisputeOrder],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.MarkAsDisputedAsync(disputeRequest, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.DisputeOrder);

		return result;
	}

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

	public async Task<bool> AddArchiveReportAsync(ArchiveReport archiveReport, CancellationToken cancellationToken)
	{
		return await _atsRepository.AddArchiveReportAsync(archiveReport, cancellationToken);
	}

	public Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		return _atsRepository.GetDashboardDataAsync(
			authorizedClientIds,
			requiredRequestorId,
			cancellationToken);
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

	public async Task<List<EmailInvitationRequest>> GetEmailInvitationRequestsNeedingProjectionAsync(CancellationToken cancellationToken)
	{
		return await _atsRepository.GetEmailInvitationRequestsNeedingProjectionAsync(cancellationToken);
	}

	public async Task<ApplicantSearchProjection?> GetApplicantSearchProjectionByIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetApplicantSearchProjectionByIdAsync(emailInvitationRequestId, cancellationToken);
	}

	public async Task<bool> AddApplicantSearchProjectionAsync(ApplicantSearchProjection projection, CancellationToken cancellationToken)
	{
		return await _atsRepository.AddApplicantSearchProjectionAsync(projection, cancellationToken);
	}

	public async Task<List<DownloadDocumentDTO>> GetDownloadDocumentsAsync(List<Guid> emailInvitationRequestIds, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetDownloadDocumentsAsync(emailInvitationRequestIds, cancellationToken);
	}
}
