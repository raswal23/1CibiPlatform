namespace ATS.DTO;

public record ApplicationFormDataDTO
{
	public PersonalDetailsDTO? PersonalDetails { get; set; }
	public AddressDetailsDTO? AddressDetails { get; set; }
	public EducationalBackgroundDTO? EducationalBackground { get; set; }
	public LicensesDetailsDTO? LicensesDetails { get; set; }
	public ProfessionalExperiencesDTO? ProfessionalExperiences { get; set; }
	public ReferenceDetailsDTO? ReferenceDetails { get; set; }
}
