namespace ATS.Data.Cache;

public class ATSCacheRepository : IATSRepository
{
	private readonly IATSRepository _atsRepository;
	private readonly HybridCache _hybridCache;

	private readonly string WithdrawnApplicationTag = "withdrawnapplication";
	private readonly string DisputeOrderTag = "disputeorder";
	private readonly string ReportTag = "report";
	private readonly string ClientTag = "client";
	private readonly string PackageTag = "package";
	private readonly string RoleTag = "role";
	private readonly string ModuleTag = "module";

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
		return await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);
	}

	public async Task<bool> AddLicensesDetailsAsync(LicensesDetails licensesDetails)
	{
		return await _atsRepository.AddLicensesDetailsAsync(licensesDetails);
	}

	public async Task<bool> AddPersonalDetailsAsync(PersonalDetails personalDetails)
	{
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
			async id => await _atsRepository.GetEmailIdAndApplicationFormPathAsync(hashToken, cancellationToken));
	}

	public async Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails)
	{
		return await _atsRepository.AddBulkUploadFileDetailsAsync(bulkUploadFileDetails);
	}

	public async Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync()
	{
		return await _atsRepository.GetBulkUploadFileDetailsAsync();
	}

	public async Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		return await _atsRepository.AddBulkEmailInvitationRequestAsync(emailInvitationRequests);
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		return await _atsRepository.UpdateBulkEmailInvitationRequestForSentEmailAsync(emailInvitationRequests);
	}

	public async Task<bool> UpdateBulkFileDetailsStatusAsync(List<BulkUploadFileDetails> bulkUploadFileDetails)
	{
		return await _atsRepository.UpdateBulkFileDetailsStatusAsync(bulkUploadFileDetails);
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

		if (result)
			await _hybridCache.RemoveByTagAsync(ReportTag);

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
		return await _atsRepository.WithdrawnApplicationForm(hashToken, cancellationToken);
	}

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"withdrawnapplication_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";


		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<EmailInvitationRequestListDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.GetWithdrawnEmailInvitationRequestsAsync(req, token),
			null,
			tags: [WithdrawnApplicationTag],
			cancellationToken);
	}

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> SearchWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"withdrawnapplication_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<EmailInvitationRequestListDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.SearchWithdrawnEmailInvitationRequestsAsync(req, token),
			null,
			tags: [WithdrawnApplicationTag],
			cancellationToken);
	}

	public async Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetEmailInvitationRequestByIdAsync(emailInvitationId, cancellationToken);
	}

	public async Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.ResendApplicationFormAsync(emailInvitationId, hashToken, hashTokenExpiration, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(WithdrawnApplicationTag);

		return result;
	}

	public async Task<bool> UpdateOrderStatusAsync(Guid EmailInvitationRequestId, string orderStatus, DateTime? orderCompletedAt, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.UpdateOrderStatusAsync(EmailInvitationRequestId, orderStatus, orderCompletedAt, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(DisputeOrderTag);
		await _hybridCache.RemoveByTagAsync(ReportTag);

		return result;
	}

	public async Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"disputeorder_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<DisputeOrderListDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.GetDisputeOrdersAsync(req, token),
			null,
			tags: [DisputeOrderTag],
			cancellationToken);
	}

	public async Task<PaginatedResult<DisputeOrderListDTO>> SearchDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"disputeorder_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<DisputeOrderListDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.SearchDisputeOrdersAsync(req, token),
			null,
			tags: [DisputeOrderTag],
			cancellationToken);
	}

	public async Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.MarkAsDisputedAsync(disputeRequest, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(DisputeOrderTag);

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
			await _hybridCache.RemoveByTagAsync(ReportTag);
		return result;
	}

	public async Task<bool> UpdateReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.UpdateReportDetailsAsync(reportDetails, cancellationToken);
		if (result)
			await _hybridCache.RemoveByTagAsync(ReportTag);
		return result;
	}

	public async Task<bool> AddArchiveReportAsync(ArchiveReport archiveReport, CancellationToken cancellationToken)
	{
		return await _atsRepository.AddArchiveReportAsync(archiveReport, cancellationToken);
	}

	public async Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, string? sortColumn, bool sortDescending, CancellationToken cancellationToken)
	{
		var cacheKey = $"report_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_sort_{sortColumn}_desc_{sortDescending}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ReportListDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.GetReportsAsync(req, sortColumn, sortDescending, token),
			null,
			tags: [ReportTag],
			cancellationToken);
	}

	public async Task<ReportResultDTO?> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		var cacheKey = $"report_result_{emailInvitationRequestId}";

		return await _hybridCache.GetOrCreateAsync(
			cacheKey,
			async _ => await _atsRepository.GetReportResultByEmailInvitationRequestIdAsync(emailInvitationRequestId, cancellationToken),
			options: new HybridCacheEntryOptions
			{
				Expiration = TimeSpan.FromMinutes(5)
			});
	}

	public async Task<PaginatedResult<ReportListDTO>> SearchReportsAsync(PaginationRequest paginationRequest, string? sortColumn, bool sortDescending, CancellationToken cancellationToken)
	{
		var cacheKey =
			$"report_page_{paginationRequest.PageIndex}" +
			$"_size_{paginationRequest.PageSize}" +
			$"_search_{paginationRequest.SearchTerm ?? "none"}" +
			$"_start_{(paginationRequest.StartDate.HasValue ? paginationRequest.StartDate.Value.ToString("yyyyMMdd") : "none")}" +
			$"_end_{(paginationRequest.EndDate.HasValue ? paginationRequest.EndDate.Value.ToString("yyyyMMdd") : "none")}" +
			$"_sort_{sortColumn ?? "none"}" +
			$"_desc_{sortDescending}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ReportListDTO>>(
			cacheKey,
			paginationRequest,
		  async (req, token) => await _atsRepository.SearchReportsAsync(req, sortColumn, sortDescending, token),
			null,
			tags: [ReportTag],
			cancellationToken);
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

	public async Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"package_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<PackageDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.GetPackagesAsync(req, token),
			null,
			tags: [PackageTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<PackageDetailsDTO>> SearchPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"package_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<PackageDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.SearchPackagesAsync(req, token),
			null,
			tags: [PackageTag],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO)
	{
		var result = await _atsRepository.AddPackageAsync(packageDTO);
		if (result)
			await _hybridCache.RemoveByTagAsync(PackageTag);
		return result;
	}

	public async Task<PackageDetails?> GetPackageAsync(Guid packageId)
	{
		return await _atsRepository.GetPackageAsync(packageId);
	}

	public async Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails)
	{
		var result = await _atsRepository.EditPackageAsync(packageDetails);
		if (result is not null)
			await _hybridCache.RemoveByTagAsync(PackageTag);
		return result ?? new PackageDetails();
	}

	public async Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"client_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ClientDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.GetClientsAsync(req, token),
			null,
			tags: [ClientTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<ClientDetailsDTO>> SearchClientsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"client_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ClientDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.SearchClientsAsync(req, token),
			null,
			tags: [ClientTag],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> AddClientAsync(AddClientDTO clientDTO)
	{
		var result = await _atsRepository.AddClientAsync(clientDTO);
		if (result)
			await _hybridCache.RemoveByTagAsync(ClientTag);
		return result;
	}

	public async Task<ClientDetails?> GetClientAsync(Guid clientId)
	{
		return await _atsRepository.GetClientAsync(clientId);
	}

	public async Task<ClientDetails> EditClientAsync(ClientDetails clientDetails)
	{
		var result = await _atsRepository.EditClientAsync(clientDetails);

		if (result is not null)
			await _hybridCache.RemoveByTagAsync(ClientTag);
		return result ?? new ClientDetails();
	}

	public async Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"role_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RoleDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.GetRolesAsync(req, token),
			null,
			tags: [RoleTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<RoleDetailsDTO>> SearchRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"role_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<RoleDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.SearchRolesAsync(req, token),
			null,
			tags: [RoleTag],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> AddRoleAsync(AddRoleDTO roleDTO)
	{
		var result = await _atsRepository.AddRoleAsync(roleDTO);
		if (result)
			await _hybridCache.RemoveByTagAsync(RoleTag);
		return result;
	}

	public async Task<RoleDetails?> GetRoleAsync(int roleId)
	{
		return await _atsRepository.GetRoleAsync(roleId);
	}

	public async Task<RoleDetails> EditRoleAsync(RoleDetails roleDetails)
	{
		var result = await _atsRepository.EditRoleAsync(roleDetails);
		if (result is not null)
			await _hybridCache.RemoveByTagAsync(RoleTag);
		return result ?? new RoleDetails();
	}

	public async Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"module_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ModuleDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.GetModulesAsync(req, token),
			null,
			tags: [ModuleTag],
			cancellationToken: cancellationToken);
	}

	public async Task<PaginatedResult<ModuleDetailsDTO>> SearchModulesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var cacheKey = $"module_page_{paginationRequest.PageIndex}_size_{paginationRequest.PageSize}_search_{paginationRequest.SearchTerm}";

		return await _hybridCache.GetOrCreateAsync<PaginationRequest, PaginatedResult<ModuleDetailsDTO>>(
			cacheKey,
			paginationRequest,
			async (req, token) => await _atsRepository.SearchModulesAsync(req, token),
			null,
			tags: [ModuleTag],
			cancellationToken: cancellationToken);
	}

	public async Task<bool> AddModuleAsync(AddModuleDTO moduleDTO)
	{
		var result = await _atsRepository.AddModuleAsync(moduleDTO);
		if (result)
			await _hybridCache.RemoveByTagAsync(ModuleTag);
		return result;
	}

	public async Task<ModuleDetails?> GetModuleAsync(int moduleId)
	{
		return await _atsRepository.GetModuleAsync(moduleId);
	}

	public async Task<ModuleDetails> EditModuleAsync(ModuleDetails moduleDetails)
	{
		var result = await _atsRepository.EditModuleAsync(moduleDetails);
		if (result is not null)
			await _hybridCache.RemoveByTagAsync(ModuleTag);
		return result ?? new ModuleDetails();
	}
}
