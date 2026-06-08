namespace ATS.Services;

public class ATSService : IATSService
{
	private readonly ILogger<ATSService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IConfiguration _configuration;
	private readonly IObjectStorageService _objectStorageService;
	private readonly string _applicationFormPath;
	private readonly string _folderName;
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
		_folderName = _configuration["ATS:ATSFolderName"] ?? "";
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
			Action = "AddingApplicationFormData",
			Step = "StartAddingApplicationFormData",
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

			var keys = new[]
			{
				resumeFileKey,
				nbiKey,
				govtIdKey,
				highSchoolDiplomaKey,
				seniorHighSchoolDiplomaKey,
				bachelorsDiplomaKey,
				mastersDiplomaKey,
				doctorateDiplomaKey,
				emp1COEKey,
				emp2COEKey,
				emp3COEKey,
				licenseKey,
				signatureKey
			};

			foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
			{
				try
				{
					await _objectStorageService.DeleteAsync(key, ct);
				}
				catch (Exception deleteEx)
				{
					_logger.LogWarning(deleteEx,
						"Failed to delete file with key {Key}", key);
				}
			}
			await _unitOfWork.RollbackAsync(ct);
			_logger.LogError("Failed Transaction: Failed to add Application Form Data record for {EmailId}: {@Context}", personalDetails.EmailInvitationID, logContext);
			throw new InternalServerException($"Failed to add transaction. {ex.InnerException!.Message}");
		}

	}

	private async Task<bool> AddPersonalDetailsDataAsync(PersonalDetailsDTO personalDetailsDTO, CancellationToken cancellationToken) 
	{
		if (personalDetailsDTO.ResumeFile != null)
		{
			await using var resumeStream = personalDetailsDTO.ResumeFile.OpenReadStream();
			resumeFileKey = await _objectStorageService.UploadAsync(_folderName, personalDetailsDTO.ResumeFile.FileName, resumeStream, cancellationToken);
		}

		if (personalDetailsDTO.NBIClearanceFile != null)
		{
			await using var nbiStream = personalDetailsDTO.NBIClearanceFile.OpenReadStream();
			nbiKey = await _objectStorageService.UploadAsync(_folderName, personalDetailsDTO.NBIClearanceFile.FileName, nbiStream, cancellationToken);
		}

		if (personalDetailsDTO.AdditionalGovtIDFile != null)
		{
			await using var govtIdStream = personalDetailsDTO.AdditionalGovtIDFile.OpenReadStream();
			govtIdKey = await _objectStorageService.UploadAsync(_folderName, personalDetailsDTO.AdditionalGovtIDFile.FileName, govtIdStream, cancellationToken);
		}

		PersonalDetails personalDetails = personalDetailsDTO.Adapt<PersonalDetails>();
		personalDetails.ResumeFileKey = resumeFileKey;
		personalDetails.NBIClearanceFileKey = nbiKey;
		personalDetails.AdditionalGovtIDFileKey = govtIdKey;
		personalDetails.CreatedDate = DateTime.UtcNow;

		bool isAdded = await _atsRepository.AddPersonalDetailsAsync(personalDetails);

		return isAdded;
	}

	private async Task<bool> AddAddressDataAsync(
		AddressDetailsDTO addressDetailsDTO,
		CancellationToken cancellationToken)
	{
		AddressDetails addressDetails = addressDetailsDTO.Adapt<AddressDetails>();
		addressDetails.CreatedDate = DateTime.UtcNow;

		bool isAdded = await _atsRepository.AddAddressDetailsAsync(addressDetails);

		return isAdded;
	}

	private async Task<bool> AddEducationalBackgroundDataAsync(
		EducationalBackgroundDTO educationalBackgroundDTO,
		CancellationToken cancellationToken)
	{
		if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("HighSchool Graduate", StringComparison.OrdinalIgnoreCase))
		{
			await using var highSchoolDiplomaStream = educationalBackgroundDTO.HighSchoolDiplomaFile!.OpenReadStream();
			highSchoolDiplomaKey = await _objectStorageService.UploadAsync(_folderName, educationalBackgroundDTO.HighSchoolDiplomaFileName!, highSchoolDiplomaStream, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Senior High School Graduate", StringComparison.OrdinalIgnoreCase))
		{
			await using var seniorHighSchoolDiplomaStream = educationalBackgroundDTO.SeniorHighSchoolDiplomaFile!.OpenReadStream();
			seniorHighSchoolDiplomaKey = await _objectStorageService.UploadAsync(_folderName, educationalBackgroundDTO.SeniorHighSchoolDiplomaFileName!, seniorHighSchoolDiplomaStream, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Bachelor's Degree", StringComparison.OrdinalIgnoreCase))
		{
			await using var bachelorsDiplomaStream = educationalBackgroundDTO.BachelorsDiplomaFile!.OpenReadStream();
			bachelorsDiplomaKey = await _objectStorageService.UploadAsync(_folderName, educationalBackgroundDTO.BachelorsDiplomaFileName!, bachelorsDiplomaStream, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Master's Degree", StringComparison.OrdinalIgnoreCase))
		{
			await using var mastersDiplomaStream = educationalBackgroundDTO.MastersDiplomaFile!.OpenReadStream();
			mastersDiplomaKey = await _objectStorageService.UploadAsync(_folderName, educationalBackgroundDTO.MastersDiplomaFileName!, mastersDiplomaStream, cancellationToken);
		}
		else if (educationalBackgroundDTO.HighestEducationalAttainment!.Contains("Doctorate Degree", StringComparison.OrdinalIgnoreCase))
		{
			await using var doctorateDiplomaStream = educationalBackgroundDTO.DoctorateDiplomaFile!.OpenReadStream();
			doctorateDiplomaKey = await _objectStorageService.UploadAsync(_folderName, educationalBackgroundDTO.DoctorateDiplomaFileName!, doctorateDiplomaStream, cancellationToken);
		}

		EducationalBackground educationalBackground = educationalBackgroundDTO.Adapt<EducationalBackground>();
		educationalBackground.HighSchoolDiplomaFileKey = highSchoolDiplomaKey;
		educationalBackground.SeniorHighSchoolDiplomaFileKey = seniorHighSchoolDiplomaKey;
		educationalBackground.BachelorsDiplomaFileKey = bachelorsDiplomaKey;
		educationalBackground.MastersDiplomaFileKey = mastersDiplomaKey;
		educationalBackground.DoctorateDiplomaFileKey = doctorateDiplomaKey;
		educationalBackground.CreatedDate = DateTime.UtcNow;

		bool isAdded = await _atsRepository.AddEducationalBackgroundAsync(educationalBackground);

		return isAdded;
	}

	private async Task<bool> AddLicensesDataAsync(
		LicensesDetailsDTO licensesDetailsDTO,
		CancellationToken cancellationToken)
	{
		await using var licenseStream = licensesDetailsDTO.LicenseUploadFile!.OpenReadStream();
		string licenseKey = await _objectStorageService.UploadAsync(licenseStream, licensesDetailsDTO.LicenseUploadFileName!, cancellationToken);

		LicensesDetails licensesDetails = licensesDetailsDTO.Adapt<LicensesDetails>();
		licensesDetails.LicenseUploadFileKey = licenseKey;
		licensesDetails.CreatedDate = DateTime.UtcNow;

		bool isAdded = await _atsRepository.AddLicensesDetailsAsync(licensesDetails);

		return isAdded;
	}

	private async Task<bool> AddProfessionalExperiencesDataAsync(
		ProfessionalExperiencesDTO professionalExperiencesDTO,
		CancellationToken cancellationToken)
	{
		if (professionalExperiencesDTO.Emp1COEUploadFile! != null)
		{
			await using var emp1COEStream = professionalExperiencesDTO.Emp1COEUploadFile!.OpenReadStream();
			emp1COEKey = await _objectStorageService.UploadAsync(_folderName, professionalExperiencesDTO.Emp1COEUploadFileName!, emp1COEStream, cancellationToken);
		}
		if (professionalExperiencesDTO.Emp2COEUploadFile! != null)
		{
			await using var emp2COEStream = professionalExperiencesDTO.Emp2COEUploadFile!.OpenReadStream();
			emp2COEKey = await _objectStorageService.UploadAsync(_folderName, professionalExperiencesDTO.Emp2COEUploadFileName!, emp2COEStream, cancellationToken);
		}
		if (professionalExperiencesDTO.Emp3COEUploadFile! != null)
		{
			await using var emp3COEStream = professionalExperiencesDTO.Emp3COEUploadFile!.OpenReadStream();
			emp3COEKey = await _objectStorageService.UploadAsync(_folderName, professionalExperiencesDTO.Emp3COEUploadFileName!, emp3COEStream, cancellationToken);
		}

		ProfessionalExperiences professionalExperiences = professionalExperiencesDTO.Adapt<ProfessionalExperiences>();
		professionalExperiences.Emp1COEUploadFileKey = emp1COEKey;
		professionalExperiences.Emp2COEUploadFileKey = emp2COEKey;
		professionalExperiences.Emp3COEUploadFileKey = emp3COEKey;
		professionalExperiences.CreatedDate = DateTime.UtcNow;
		bool isAdded = await _atsRepository.AddProfessionalExperiencesAsync(professionalExperiences);
		return isAdded;
	}

	private async Task<bool> AddReferenceDetailsDataAsync(
		ReferenceDetailsDTO referenceDetailsDTO,
		CancellationToken cancellationToken)
	{
		ReferenceDetails referenceDetails = referenceDetailsDTO.Adapt<ReferenceDetails>();

		// Ensure all DateTime values are UTC
		if (referenceDetails.Ref1BestTimeToContact.HasValue)
		{
			referenceDetails.Ref1BestTimeToContact = DateTime.SpecifyKind(referenceDetails.Ref1BestTimeToContact.Value, DateTimeKind.Utc);
		}
		if (referenceDetails.Ref2BestTimeToContact.HasValue)
		{
			referenceDetails.Ref2BestTimeToContact = DateTime.SpecifyKind(referenceDetails.Ref2BestTimeToContact.Value, DateTimeKind.Utc);
		}
		if (referenceDetails.Ref3BestTimeToContact.HasValue)
		{
			referenceDetails.Ref3BestTimeToContact = DateTime.SpecifyKind(referenceDetails.Ref3BestTimeToContact.Value, DateTimeKind.Utc);
		}

		referenceDetails.CreatedDate = DateTime.UtcNow;

		bool isAdded = await _atsRepository.AddReferenceDetailsAsync(referenceDetails);
		return isAdded;
	}

	private async Task<bool> AddSignatureDetailsDataAsync(
		SignatureDetailsDTO signatureDetailsDTO,
		CancellationToken cancellationToken)
	{
		if (signatureDetailsDTO.Signature != null)
		{
			await using var signatureStream = signatureDetailsDTO.Signature.OpenReadStream();
			signatureKey = await _objectStorageService.UploadAsync(_folderName, signatureDetailsDTO.SignatureFileName!, signatureStream, cancellationToken);
		}
		SignatureDetails signatureDetails = signatureDetailsDTO.Adapt<SignatureDetails>();
		signatureDetails.SignatureFileKey = signatureKey;
		bool isAdded = await _atsRepository.AddSignatureDetailsAsync(signatureDetails);
		return isAdded;
	}

	public async Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(
		string hashToken,
		CancellationToken ct = default)
	{
		var logContext = new
		{
			Action = "GettingEmailIdAndApplicationFormPath",
			Step = "StartFetchingEmailIdAndApplicationFormPath",
			Identity = hashToken,
			Timestamp = DateTime.UtcNow
		};

		EmailIdAndApplicationFormPathDTO emailIdAndApplicationFormPath = await _atsRepository.GetEmailIdAndApplicationFormPathAsync(hashToken, ct);

		if (emailIdAndApplicationFormPath.EmailId == Guid.Empty)
		{
			_logger.LogError("Failed Transaction: Failed to fetch EmailId and Application Form Path for {HashToken}: {@Context}", hashToken, logContext);
			throw new NotFoundException("No record found for the provided hash token.");
		}

		_logger.LogInformation("Succcessfully fetched the EmailId and Application Form Path for {EmailId}: {@Context}", emailIdAndApplicationFormPath.EmailId, logContext);

		emailIdAndApplicationFormPath.ApplicationFormPath = _applicationFormPath;
		return emailIdAndApplicationFormPath;
	}
}
