namespace ATS.Data.Cache;

public class ATSCacheRepository : IATSRepository
{
	private readonly IATSRepository _atsRepository;
	private readonly HybridCache _hybridCache;

	private readonly string WithdrawnApplicationTag = "withdrawnapplication";
	private readonly string DisputeOrderTag = "disputeorder";

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
		return await _atsRepository.UpdateEmailInvitationRequestForFilledUpFormAsync(emailInvitationRequestId);
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
}
