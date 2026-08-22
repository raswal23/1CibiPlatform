namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public async Task<bool> AddAddressDetailsAsync(AddressDetails addressDetails)
	{
		return await _atsRepository.AddAddressDetailsAsync(addressDetails);
	}

	public async Task<bool> AddEducationalBackgroundAsync(EducationalBackground educationalBackground)
	{
		return await _atsRepository.AddEducationalBackgroundAsync(educationalBackground);
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

	// Pure passthrough on purpose - see the repository comment: caching this would let a
	// stale expiry or form status keep a spent link working.
	public async Task<ApplicationFormClaimDTO?> GetApplicationFormClaimAsync(string hashToken, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetApplicationFormClaimAsync(hashToken, cancellationToken);
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

	public async Task<int> WithdrawnApplicationForm(string hashToken, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.WithdrawnApplicationForm(hashToken, cancellationToken);
		await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication);
		return result;
	}
}
