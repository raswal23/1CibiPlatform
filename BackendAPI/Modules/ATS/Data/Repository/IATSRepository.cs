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
	Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateEmailInvitationRequestForFilledUpFormAsync(Guid emailInvitationRequestId);
	Task<bool> UpdateBulkFileDetailsStatusAsync(List<BulkUploadFileDetails> bulkUploadFileDetails);
	Task<bool> UpdateEmailInvitationRequestStatusAsync(Guid emailInvitationId, string status);
	Task<string?> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken);
}
