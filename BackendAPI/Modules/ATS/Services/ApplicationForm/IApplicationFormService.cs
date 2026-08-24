namespace ATS.Services.ApplicationForm;

public interface IApplicationFormService
{
	/// <summary>
	/// Persists a candidate's application form. The invitation is resolved from
	/// <paramref name="hashToken"/>; any EmailInvitationID carried on the DTOs is
	/// overwritten and never trusted.
	/// </summary>
	Task<bool> AddApplicationFormDataAsync(string hashToken,
										   PersonalDetailsDTO personalDetails,
										   AddressDetailsDTO addressDetails,
										   EducationalBackgroundDTO educationalBackground,
										   LicensesDetailsDTO licensesDetails,
										   ProfessionalExperiencesDTO professionalExperiences,
										   ReferenceDetailsDTO referenceDetails,
										   SignatureDetailsDTO signatureDetails,
										   CancellationToken ct = default);

	Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string hashToken,
																				 CancellationToken ct = default);

	Task<bool> WithdrawnApplicationForm(string hashToken, CancellationToken ct = default);
}
