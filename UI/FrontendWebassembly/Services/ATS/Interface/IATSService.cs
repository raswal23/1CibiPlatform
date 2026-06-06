namespace FrontendWebassembly.Services.ATS.Interface;

public interface IATSService
{
	Task<bool> AddApplicationFormDataAsync(PersonalDetailsDTO PersonalDetails,
											AddressDetailsDTO AddressDetails,
											EducationalBackgroundDTO EducationalBackground,
											LicensesDetailsDTO LicensesDetails,
											ProfessionalExperiencesDTO ProfessionalExperiences,
											ReferenceDetailsDTO ReferenceDetails,
											SignatureDetailsDTO SignatureDetails);

	Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string HashToken);
}
