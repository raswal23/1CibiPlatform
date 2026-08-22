namespace FrontendWebassembly.Services.ATS.ApplicationForm;

public interface IApplicationFormService
{
	Task<ServiceResponse<bool>> AddApplicationFormDataAsync(string HashToken,
											PersonalDetailsDTO PersonalDetails,
											AddressDetailsDTO AddressDetails,
											EducationalBackgroundDTO EducationalBackground,
											LicensesDetailsDTO LicensesDetails,
											ProfessionalExperiencesDTO ProfessionalExperiences,
											ReferenceDetailsDTO ReferenceDetails,
											SignatureDetailsDTO SignatureDetails);

	Task<ServiceResponse<EmailIdAndApplicationFormPathDTO>> GetEmailIdAndApplicationFormPathAsync(string HashToken);

	Task<ServiceResponse<bool>> WithdrawApplicationForm(string HashToken);
}
