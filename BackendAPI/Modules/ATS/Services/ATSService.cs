namespace ATS.Services;

public class ATSService : IATSService
{
	private readonly ILogger<ATSService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IConfiguration _configuration;
	private readonly IObjectStorageService _objectStorageService;
	private readonly string _applicationFormPath;

	public ATSService(ILogger<ATSService> logger, 
					  IATSRepository atsRepository,
					  IUnitOfWork unitOfWork,
					  IConfiguration configuration,
					  IObjectStorageService objectStorageService)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_unitOfWork = unitOfWork;
		_configuration = configuration;
		_objectStorageService = objectStorageService;
		_applicationFormPath = _configuration["ATS:ApplicationFormPath"] ?? "";
	}

	public async Task<bool> AddApplicationFormDataAsync(PersonalDetailsDTO personalDetails, 
												        AddressDetailsDTO addressDetails, 
														EducationalBackgroundDTO educationalBackground, 
														LicensesDetailsDTO licensesDetails, 
														ProfessionalExperiencesDTO professionalExperiences, 
														ReferenceDetailsDTO referenceDetails,
														SignatureDetailsDTO signatureDetails,
														CancellationToken ct = default)
	{
		var logContext = new
		{
			Action = "GettingLivenessLink",
			Step = "StartPostingPcnOrBasicInfo",
			Identity = personalDetails.EmailInvitationID,
			Timestamp = DateTime.UtcNow
		};

		await _unitOfWork.BeginTransactionAsync(ct);

		try
		{
			await AddPersonalDetailsDataAsync(personalDetails, ct);

			await AddAddressDataAsync(addressDetails, ct);

			await AddEducationalBackgroundDataAsync(educationalBackground!, ct);

			await AddLicensesDataAsync(licensesDetails!, ct);

			await AddProfessionalExperiencesDataAsync(professionalExperiences!, ct);

			await AddReferenceDetailsDataAsync(referenceDetails!, ct);

			await AddSignatureDetailsDataAsync(signatureDetails!, ct);

			await _unitOfWork.CommitAsync(ct);

		_logger.LogInformation("Succcessfully added the Application Form Data for {EmailId}: {@Context}", personalDetails.EmailInvitationID, logContext);

			return true;
		}
		catch (Exception ex)
		{
			await _unitOfWork.RollbackAsync(ct);
			_logger.LogError("Failed Transaction: Failed to add Application Form Data record for {EmailId}: {@Context}", personalDetails.EmailInvitationID, logContext);
			throw new InternalServerException($"Failed to add transaction. {ex.Message}"); 
		}

	}

	private async Task<bool> AddPersonalDetailsDataAsync(PersonalDetailsDTO personalDetailsDTO, CancellationToken cancellationToken) 
	{
		string resumeFileKey = "";
		string nbiKey = "";
		string govtIdKey = "";

		if (personalDetailsDTO.ResumeFile != null)
		{
			await using var resumeStream = personalDetailsDTO.ResumeFile.OpenReadStream();
			resumeFileKey = await _objectStorageService.UploadAsync(resumeStream, personalDetailsDTO.ResumeFile.FileName, cancellationToken);
		}

		if (personalDetailsDTO.NBIClearanceFile != null)
		{
			await using var nbiStream = personalDetailsDTO.NBIClearanceFile.OpenReadStream();
			nbiKey = await _objectStorageService.UploadAsync(nbiStream, personalDetailsDTO.NBIClearanceFile.FileName, cancellationToken);
		}

		if (personalDetailsDTO.AdditionalGovtIDFile != null)
		{
			await using var govtIdStream = personalDetailsDTO.AdditionalGovtIDFile.OpenReadStream();
			govtIdKey = await _objectStorageService.UploadAsync(govtIdStream, personalDetailsDTO.AdditionalGovtIDFile.FileName, cancellationToken);
		}

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
		string highSchoolDiplomaKey = "";
		string seniorHighSchoolDiplomaKey = "";
		string bachelorsDiplomaKey = "";
		string mastersDiplomaKey = "";
		string doctorateDiplomaKey = "";

		if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("HighSchool Graduate", StringComparison.OrdinalIgnoreCase))
		{
			await using var highSchoolDiplomaStream = educationalBackgroundDTO.HighSchoolDiplomaFile!.OpenReadStream();
			highSchoolDiplomaKey = await _objectStorageService.UploadAsync(highSchoolDiplomaStream, educationalBackgroundDTO.HighSchoolDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Senior High School Graduate", StringComparison.OrdinalIgnoreCase))
		{
			await using var seniorHighSchoolDiplomaStream = educationalBackgroundDTO.SeniorHighSchoolDiplomaFile!.OpenReadStream();
			seniorHighSchoolDiplomaKey = await _objectStorageService.UploadAsync(seniorHighSchoolDiplomaStream, educationalBackgroundDTO.SeniorHighSchoolDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Bachelor's Degree", StringComparison.OrdinalIgnoreCase))
		{
			await using var bachelorsDiplomaStream = educationalBackgroundDTO.BachelorsDiplomaFile!.OpenReadStream();
			bachelorsDiplomaKey = await _objectStorageService.UploadAsync(bachelorsDiplomaStream, educationalBackgroundDTO.BachelorsDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Master's Degree", StringComparison.OrdinalIgnoreCase))
		{
			await using var mastersDiplomaStream = educationalBackgroundDTO.MastersDiplomaFile!.OpenReadStream();
			mastersDiplomaKey = await _objectStorageService.UploadAsync(mastersDiplomaStream, educationalBackgroundDTO.MastersDiplomaFileName!, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Doctorate Degree", StringComparison.OrdinalIgnoreCase))
		{
			await using var doctorateDiplomaStream = educationalBackgroundDTO.DoctorateDiplomaFile!.OpenReadStream();
			doctorateDiplomaKey = await _objectStorageService.UploadAsync(doctorateDiplomaStream, educationalBackgroundDTO.DoctorateDiplomaFileName!, cancellationToken);
		}

		EducationalBackground educationalBackground = educationalBackgroundDTO.Adapt<EducationalBackground>();
		educationalBackground.HighSchoolDiplomaFileKey = highSchoolDiplomaKey;
		educationalBackground.SeniorHighSchoolDiplomaFileKey = seniorHighSchoolDiplomaKey;
		educationalBackground.BachelorsDiplomaFileKey = bachelorsDiplomaKey;
		educationalBackground.MastersDiplomaFileKey = mastersDiplomaKey;
		educationalBackground.DoctorateDiplomaFileKey = doctorateDiplomaKey;

		bool isAdded = await _atsRepository.AddEducationalBackgroundAsync(educationalBackground);
		return true;
	}

	private async Task<bool> AddLicensesDataAsync(LicensesDetailsDTO licensesDetailsDTO, CancellationToken cancellationToken)
	{
		await using var licenseStream = licensesDetailsDTO.LicenseUploadFile!.OpenReadStream();
		string licenseKey = await _objectStorageService.UploadAsync(licenseStream, licensesDetailsDTO.LicenseUploadFileName!, cancellationToken);

		LicensesDetails licensesDetails = licensesDetailsDTO.Adapt<LicensesDetails>();
		licensesDetails.LicenseUploadFileKey = licenseKey;
		bool isAdded = await _atsRepository.AddLicensesDetailsAsync(licensesDetails);
		return true;
	}

	private async Task<bool> AddProfessionalExperiencesDataAsync(ProfessionalExperiencesDTO professionalExperiencesDTO, CancellationToken cancellationToken)
	{
		string emp1COEKey = "";
		string emp2COEKey = "";
		string emp3COEKey = "";

		if (professionalExperiencesDTO.Emp1COEUploadFile! != null)
		{
			await using var emp1COEStream = professionalExperiencesDTO.Emp1COEUploadFile!.OpenReadStream();
			emp1COEKey = await _objectStorageService.UploadAsync(emp1COEStream, professionalExperiencesDTO.Emp1COEUploadFileName!, cancellationToken);
		}
		if (professionalExperiencesDTO.Emp2COEUploadFile! != null)
		{
			await using var emp2COEStream = professionalExperiencesDTO.Emp2COEUploadFile!.OpenReadStream();
			emp2COEKey = await _objectStorageService.UploadAsync(emp2COEStream, professionalExperiencesDTO.Emp2COEUploadFileName!, cancellationToken);
		}
		if (professionalExperiencesDTO.Emp3COEUploadFile! != null)
		{
			await using var emp3COEStream = professionalExperiencesDTO.Emp3COEUploadFile!.OpenReadStream();
			emp3COEKey = await _objectStorageService.UploadAsync(emp3COEStream, professionalExperiencesDTO.Emp3COEUploadFileName!, cancellationToken);
		}

		ProfessionalExperiences professionalExperiences = professionalExperiencesDTO.Adapt<ProfessionalExperiences>();
		professionalExperiences.Emp1COEUploadFileKey = emp1COEKey;
		professionalExperiences.Emp2COEUploadFileKey = emp2COEKey;
		professionalExperiences.Emp3COEUploadFileKey = emp3COEKey;
		bool isAdded = await _atsRepository.AddProfessionalExperiencesAsync(professionalExperiences);
		return true;
	}

	private async Task<bool> AddReferenceDetailsDataAsync(ReferenceDetailsDTO referenceDetailsDTO, CancellationToken cancellationToken)
	{
		ReferenceDetails referenceDetails = referenceDetailsDTO.Adapt<ReferenceDetails>();
		bool isAdded = await _atsRepository.AddReferenceDetailsAsync(referenceDetails);
		return true;
	}

	private async Task<bool> AddSignatureDetailsDataAsync(SignatureDetailsDTO signatureDetailsDTO, CancellationToken cancellationToken)
	{
		string signatureKey = "";
		if (signatureDetailsDTO.Signature != null)
		{
			await using var signatureStream = signatureDetailsDTO.Signature.OpenReadStream();
			signatureKey = await _objectStorageService.UploadAsync(signatureStream, signatureDetailsDTO.SignatureFileName!, cancellationToken);
		}
		SignatureDetails signatureDetails = signatureDetailsDTO.Adapt<SignatureDetails>();
		signatureDetails.SignatureFileKey = signatureKey;
		bool isAdded = await _atsRepository.AddSignatureDetailsAsync(signatureDetails);
		return true;
	}

	public async Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string hashToken, CancellationToken ct = default)
	{
		Guid emailId = await _atsRepository.GetEmailIdAndApplicationFormPathAsync(hashToken, ct);
		return new EmailIdAndApplicationFormPathDTO
		{
			EmailId = emailId,
			ApplicationFormPath = _applicationFormPath
		};
	}
}
	