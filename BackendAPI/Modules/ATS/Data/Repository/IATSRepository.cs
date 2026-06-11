namespace ATS.Data.Repository;

public interface IATSRepository
{
	Task<bool> AddPersonalDetailsAsync(PersonalDetails personalDetails);
	Task<bool> AddAddressDetailsAsync(AddressDetails addressDetails);
	Task<bool> AddEducationalBackgroundAsync(EducationalBackground educationalBackground);
	Task<bool> AddLicensesDetailsAsync(LicensesDetails licensesDetails);
	Task<bool> AddProfessionalExperiencesAsync(ProfessionalExperiences professionalExperiences);
	Task<bool> AddReferenceDetailsAsync(ReferenceDetails referenceDetails);
	Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string hashToken,
												 CancellationToken cancellationToken);
	Task<bool> AddSignatureDetailsAsync(SignatureDetails signatureDetails);
	Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest);
	Task<bool>AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails);

	Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync();
	Task<bool> UpdateBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateEmailInvitationRequestForSuccessAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateEmailInvitationRequestForErrorAsync(List<EmailInvitationRequest> emailInvitationRequests);

}
