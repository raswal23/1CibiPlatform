namespace ATS.Data.Repository;

public partial class ATSRepository
{
	public async Task<bool> AddPersonalDetailsAsync(PersonalDetails personalDetails)
	{
		await _dbcontext.PersonalDetails.AddAsync(personalDetails);
		return true;
	}

	public async Task<bool> AddAddressDetailsAsync(AddressDetails addressDetails)
	{
		await _dbcontext.AddressDetails.AddAsync(addressDetails);
		return true;
	}

	public async Task<bool> AddEducationalBackgroundAsync(EducationalBackground educationalBackground)
	{
		await _dbcontext.EducationalBackgrounds.AddAsync(educationalBackground);
		return true;
	}

	public async Task<bool> AddLicensesDetailsAsync(LicensesDetails licensesDetails)
	{
		await _dbcontext.LicensesDetails.AddAsync(licensesDetails);
		return true;
	}

	public async Task<bool> AddProfessionalExperiencesAsync(ProfessionalExperiences professionalExperiences)
	{
		await _dbcontext.ProfessionalExperiences.AddAsync(professionalExperiences);
		return true;
	}

	public async Task<bool> AddReferenceDetailsAsync(ReferenceDetails referenceDetails)
	{
		await _dbcontext.ReferenceDetails.AddAsync(referenceDetails);
		return true;
	}

	public async Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string hashToken,
						CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
				.AsNoTracking()
				.Where(af => af.HashToken == hashToken)
				.Select(af => new EmailIdAndApplicationFormPathDTO
				{
					EmailId = af.EmailInvitationID,
					Status = af.ApplicationFormStatus,
					DateOfBirth = af.DateOfBirth
				})
				.FirstOrDefaultAsync(cancellationToken) ?? new EmailIdAndApplicationFormPathDTO();
	}

	// Deliberately not cached: this is the authorization decision for the anonymous
	// application-form endpoints, so a stale form status would keep a spent link working.
	public async Task<ApplicationFormClaimDTO?> GetApplicationFormClaimAsync(string hashToken,
						CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(hashToken))
			return null;

		return await _dbcontext.EmailInvitationRequests
				.AsNoTracking()
				.Where(eir => eir.HashToken == hashToken)
				.Select(eir => new ApplicationFormClaimDTO
				{
					EmailInvitationID = eir.EmailInvitationID,
					ApplicationFormStatus = eir.ApplicationFormStatus
				})
				.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<bool> AddSignatureDetailsAsync(SignatureDetails signatureDetails)
	{
		await _dbcontext.SignatureDetails.AddAsync(signatureDetails);
		return true;
	}

	// Answers "is this token still usable", which PhilSys asks before accepting an ATS
	// session. The link no longer expires, so the form's status carries the whole answer -
	// and it has to carry something: existence alone would make this permanently true the
	// moment the expiry clause came out, which would hand a submitted or withdrawn form a
	// live session forever. Pending is the same state AuthorizeApplicationFormAsync
	// demands, so the two agree by construction.
	public async Task<bool> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.AnyAsync(eir => eir.HashToken == hashToken &&
					  eir.ApplicationFormStatus == ApplicationFormStatus.Pending,
					  cancellationToken);
	}

	public async Task<bool> UpdateEmailInvitationRequestForFilledUpFormAsync(Guid emailInvitationRequestId)
	{

		await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == emailInvitationRequestId)
			.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.ApplicationFormStatus, x => ApplicationFormStatus.Done)
			.SetProperty(x => x.FormCompletedAt, x => DateTime.UtcNow)
			.SetProperty(
				x => x.OrderStatus,
				x => x.OrderStatus == OrderStatus.Completed
					? x.OrderStatus
					: OrderStatus.InProgress)
			.SetProperty(x => x.NeedsProjection, x => true));

		return true;
	}

	public async Task<int> WithdrawnApplicationForm(string hashToken, CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests.Where(x => x.HashToken == hashToken)
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.ApplicationFormStatus, x => ApplicationFormStatus.Withdrawn)
				.SetProperty(x => x.OrderStatus, x => OrderStatus.ApplicationWithdrawn));
	}
}
