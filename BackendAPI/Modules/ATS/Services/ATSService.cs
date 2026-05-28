using ATS.Data.Repository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace ATS.Services;

public class ATSService : IATSService
{
	private readonly ILogger<ATSService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IObjectStorageService _objectStorageService;

	public ATSService(ILogger<ATSService> logger, 
					  IATSRepository atsRepository,
					  IObjectStorageService objectStorageService)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_objectStorageService = objectStorageService;
	}

	public async Task<bool> AddApplicationFormDataAsync(PersonalDetailsDTO personalDetails, AddressDetailsDTO addressDetails, EducationalBackgroundDTO educationalBackground, LicensesDetailsDTO licensesDetails, ProfessionalExperiencesDTO professionalExperiences, ReferenceDetailsDTO referenceDetails, CancellationToken ct = default)
	{
		await AddPersonalDetailsDataAsync(personalDetails, ct);

		await AddAddressDataAsync(addressDetails, ct);

		await AddEducationalBackgroundDataAsync(educationalBackground!, ct);

		await AddLicensesDataAsync(licensesDetails!, ct);

		await AddProfessionalExperiencesDataAsync(professionalExperiences!, ct);

		await AddReferenceDetailsDataAsync(referenceDetails!, ct);
		return true;
	}

	private async Task<bool> AddPersonalDetailsDataAsync(PersonalDetailsDTO personalDetailsDTO, CancellationToken cancellationToken) 
	{
		using var resumeStream = new MemoryStream(personalDetailsDTO.ResumeFile!);
		string resumeFileKey = await _objectStorageService.UploadAsync(resumeStream, personalDetailsDTO.ResumeFileName!, cancellationToken);

		using var nbiStream = new MemoryStream(personalDetailsDTO.NBIClearanceFile!);
		string nbiKey = await _objectStorageService.UploadAsync(nbiStream, personalDetailsDTO.NBIClearanceFileName!, cancellationToken);

		using var govtIdStream = new MemoryStream(personalDetailsDTO.AdditionalGovtIDFile!);
		string govtIdKey = await _objectStorageService.UploadAsync(govtIdStream, personalDetailsDTO.AdditionalGovtIDFileName!, cancellationToken);

		PersonalDetails personalDetails = personalDetailsDTO.Adapt<PersonalDetails>();
		personalDetails.ResumeFileKey = resumeFileKey;
		personalDetails.NBIClearanceFileKey = nbiKey;
		personalDetails.AdditionalGovtIDFileKey = govtIdKey;

		bool isAdded = await _atsRepository.AddPersonalDetailsAsync(personalDetails);

		return true;
	}

	private async Task<bool> AddAddressDataAsync(AddressDetailsDTO addressDetailsDTO, CancellationToken cancellationToken)
	{
		AddressDetails addressDetails = addressDetailsDTO.Adapt<AddressDetails>();

		bool isAdded = await _atsRepository.AddAddressDetailsAsync(addressDetails);

		return true;
	}

	private async Task<bool> AddEducationalBackgroundDataAsync(EducationalBackgroundDTO educationalBackgroundDTO, CancellationToken cancellationToken)
	{
		if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("HighSchool Graduate", StringComparison.OrdinalIgnoreCase))
		{
			using var highSchoolDiplomaStream = new MemoryStream(educationalBackgroundDTO.HighSchoolDiplomaFile!);
			string highSchoolDiplomaKey = await _objectStorageService.UploadAsync(highSchoolDiplomaStream, educationalBackgroundDTO.HighSchoolDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Senior High School Graduate", StringComparison.OrdinalIgnoreCase))
		{
			using var seniorHighSchoolDiplomaStream = new MemoryStream(educationalBackgroundDTO.SeniorHighSchoolDiplomaFile!);
			string seniorHighSchoolDiplomaKey = await _objectStorageService.UploadAsync(seniorHighSchoolDiplomaStream, educationalBackgroundDTO.SeniorHighSchoolDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Bachelor's Degree", StringComparison.OrdinalIgnoreCase))
		{
			using var bachelorsDiplomaStream = new MemoryStream(educationalBackgroundDTO.BachelorsDiplomaFile!);
			string bachelorsDiplomaKey = await _objectStorageService.UploadAsync(bachelorsDiplomaStream, educationalBackgroundDTO.BachelorsDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Master's Degree", StringComparison.OrdinalIgnoreCase))
		{
			using var mastersDiplomaStream = new MemoryStream(educationalBackgroundDTO.MastersDiplomaFile!);
			string mastersDiplomaKey = await _objectStorageService.UploadAsync(mastersDiplomaStream, educationalBackgroundDTO.MastersDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Doctorate Degree", StringComparison.OrdinalIgnoreCase))
		{
			using var doctorateDiplomaStream = new MemoryStream(educationalBackgroundDTO.DoctorateDiplomaFile!);
			string doctorateDiplomaKey = await _objectStorageService.UploadAsync(doctorateDiplomaStream, educationalBackgroundDTO.DoctorateDiplomaFileName!, cancellationToken);
		}

		EducationalBackground educationalBackground = educationalBackgroundDTO.Adapt<EducationalBackground>();
		bool isAdded = await _atsRepository.AddEducationalBackgroundAsync(educationalBackground);
		return true;
	}

	private async Task<bool> AddLicensesDataAsync(LicensesDetailsDTO licensesDetailsDTO, CancellationToken cancellationToken)
	{
		using var licenseStream = new MemoryStream(licensesDetailsDTO.LicenseUploadFile!);
		string licenseKey = await _objectStorageService.UploadAsync(licenseStream, licensesDetailsDTO.LicenseUploadFileName!, cancellationToken);

		LicensesDetails licensesDetails = licensesDetailsDTO.Adapt<LicensesDetails>();
		bool isAdded = await _atsRepository.AddLicensesDetailsAsync(licensesDetails);
		return true;
	}

	private async Task<bool> AddProfessionalExperiencesDataAsync(ProfessionalExperiencesDTO professionalExperiencesDTO, CancellationToken cancellationToken)
	{
		ProfessionalExperiences professionalExperiences = professionalExperiencesDTO.Adapt<ProfessionalExperiences>();
		bool isAdded = await _atsRepository.AddProfessionalExperiencesAsync(professionalExperiences);
		return true;
	}

	private async Task<bool> AddReferenceDetailsDataAsync(ReferenceDetailsDTO referenceDetailsDTO, CancellationToken cancellationToken)
	{
		ReferenceDetails referenceDetails = referenceDetailsDTO.Adapt<ReferenceDetails>();
		bool isAdded = await _atsRepository.AddReferenceDetailsAsync(referenceDetails);
		return true;
	}
}
