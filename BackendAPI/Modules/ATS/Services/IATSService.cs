namespace ATS.Services;

public interface IATSService
{
	Task<bool> AddApplicationFormDataAsync(PersonalDetailsDTO personalDetails, AddressDetailsDTO addressDetails, EducationalBackgroundDTO educationalBackground, LicensesDetailsDTO licensesDetails, ProfessionalExperiencesDTO professionalExperiences, ReferenceDetailsDTO referenceDetails, CancellationToken ct = default);
}
