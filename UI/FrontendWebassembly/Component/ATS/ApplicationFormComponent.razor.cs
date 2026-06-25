namespace FrontendWebassembly.Component.ATS;

public partial class ApplicationFormComponent
{
	private MudForm? ApplicationForm;
	[Parameter]
	public Guid EmailId { get; set; }
	[Parameter]
	public bool ShowsPhilSys { get; set; } = false;
	[Parameter]
	public int ActiveStep { get; set; } = 1;
	[Parameter]
	public string? HashToken { get; set; }
	private string? FaceUrl;
	private bool IsSuccess = false;
	private bool hasProfessionalLicense = false;

	// Stepper and general
	private MudStepper? _stepper;
	private int _activeStep;
	private bool showPhilSys = false;
	private bool isSaving = false;

	// Personal Detials
	private bool consent { get; set; } = false;
	private bool declineConsent = false;
	private PersonalDetailsDTO personalDetails = new();
	private bool NoMiddleName = false;
	private DateTime? DateOfBirth;

	// AddressDetials
	private AddressDetailsDTO addressDetails = new();
	private bool SameAsPermanent;
	private string? OwnershipOtherText = null;

	// Educational background
	private EducationalBackgroundDTO educationalBackground = new();
	private DateTime? GraduationDate;
	private string? DegreeWithMajor;
	private string? AcademicInstitution;

	// Step 4 - credentials & experience
	private LicensesDetailsDTO licensesDetails = new();
	private DateTime? LicenseExpiryDate;

	private ProfessionalExperiencesDTO professionalExperiences = new();
	private DateTime? DatePermittedToContact1;
	private DateTime? StartOfEmployment1;
	private DateTime? EndOfEmployment1;
	private DateTime? DatePermittedToContact2;
	private DateTime? StartOfEmployment2;
	private DateTime? EndOfEmployment2;
	private DateTime? DatePermittedToContact3;
	private DateTime? StartOfEmployment3;
	private DateTime? EndOfEmployment3;
	private bool AddEmployer2;
	private bool AddEmployer3;

	// Step 5 - references
	private ReferenceDetailsDTO referenceDetails = new();
	private DateTime? Ref1BestDate;
	private TimeSpan? Ref1BestTime;
	private DateTime? Ref2BestDate;
	private TimeSpan? Ref2BestTime;
	private DateTime? Ref3BestDate;
	private TimeSpan? Ref3BestTime;
	private bool AddAnotherReference;

	//Final
	private SignatureDetailsDTO signatureDetails = new();
	private DateTime? SignatureDate = DateTime.UtcNow;
	private bool _signatureError;

	protected override async Task OnInitializedAsync()
	{
		personalDetails.FirstName = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_firstName") ?? string.Empty;
		personalDetails.MiddleName = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_middleName") ?? string.Empty;
		personalDetails.LastName = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_lastName") ?? string.Empty;
		personalDetails.Suffix = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_suffix") ?? string.Empty;
		string? dobString = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_birthDate");
		personalDetails.Sex = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_sex") ?? string.Empty;
		personalDetails.EmailAlternative = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_emailAddress") ?? string.Empty;
		personalDetails.MobileNumber = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_phoneNumber") ?? string.Empty;
		FaceUrl = await LocalStorageService.GetItemAsync<string?>($"{HashToken}_profilePicture") ?? string.Empty;

		if (!string.IsNullOrWhiteSpace(dobString))
		{
			if (DateTime.TryParse(dobString, out var dobDateTime))
			{
				personalDetails.DOB = DateOnly.FromDateTime(dobDateTime);
			}
		}
	}

	private void DisablePhilSys()
	{
		showPhilSys = false;
		ActiveStep = 2; 
	}

	private void RemoveFileFromUploadsAsync(string fileName)
	{
		// Personal Details
		if (fileName == personalDetails.AdditionalGovtIDFileName)
		{
			personalDetails.AdditionalGovtIDFile = null;
			personalDetails.AdditionalGovtIDFileName = null;
			return;
		}

		if (fileName == personalDetails.NBIClearanceFileName)
		{
			personalDetails.NBIClearanceFile = null;
			personalDetails.NBIClearanceFileName = null;
			return;
		}

		if (fileName == personalDetails.ResumeFileName)
		{
			personalDetails.ResumeFile = null;
			personalDetails.ResumeFileName = null;
			return;
		}

		// Educational Background
		if (fileName == educationalBackground.DiplomaFileName)
		{
			educationalBackground.DiplomaFile = null;
			educationalBackground.DiplomaFileName = null;
			return;
		}

		// Licenses
		if (fileName == licensesDetails.LicenseUploadFileName)
		{
			licensesDetails.LicenseUploadFile = null;
			licensesDetails.LicenseUploadFileName = null;
			return;
		}

		// Experience
		if (fileName == professionalExperiences.Emp1COEUploadFileName)
		{
			professionalExperiences.Emp1COEUploadFile = null;
			professionalExperiences.Emp1COEUploadFileName = null;
			return;
		}

		if (fileName == professionalExperiences.Emp2COEUploadFileName)
		{
			professionalExperiences.Emp2COEUploadFile = null;
			professionalExperiences.Emp2COEUploadFileName = null;
			return;
		}

		if (fileName == professionalExperiences.Emp3COEUploadFileName)
		{
			professionalExperiences.Emp3COEUploadFile = null;
			professionalExperiences.Emp3COEUploadFileName = null;
			return;
		}
	}

	private async Task SkipStep()
	{
		if (_stepper is not null)
			await _stepper.SkipCurrentStepAsync();
	}

	private async Task ResetStepper()
	{
		if (_stepper is not null)
			await _stepper.ResetAsync();
	}

	private async Task ProceedClicked()
	{
		showPhilSys = true;
	}

	private async Task OnGovtIdUpload(InputFileChangeEventArgs e)
	{

		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.AdditionalGovtIDFile = ms.ToArray();
			personalDetails.AdditionalGovtIDFileName = e.File!.Name;
		}

		return;
	}

	private async Task OnNbiUpload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.NBIClearanceFile = ms.ToArray();
			personalDetails.NBIClearanceFileName = e.File.Name;
		}
		return;
	}

	private async Task OnCvUpload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.ResumeFile = ms.ToArray();
			personalDetails.ResumeFileName = e.File.Name;
		}
		return;
	}

	private async Task OnDiplomaUpload(InputFileChangeEventArgs e)
	{
		if (e.File is not null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			educationalBackground.DiplomaFile = ms.ToArray();
			educationalBackground.DiplomaFileName = e.File!.Name;
		}

		return;
	}

	private async Task OnProfessionalLicenseUpload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			licensesDetails.LicenseUploadFile = ms.ToArray();
			licensesDetails.LicenseUploadFileName = e.File.Name;
		}

		return;
	}

	private async Task OnCoe1Upload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			professionalExperiences.Emp1COEUploadFile = ms.ToArray();
			professionalExperiences.Emp1COEUploadFileName = e.File.Name;
		}

		return;
	}

	private async Task OnCoe2Upload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			professionalExperiences.Emp2COEUploadFile = ms.ToArray();
			professionalExperiences.Emp2COEUploadFileName = e.File.Name;
		}

		return;
	}

	private async Task OnCoe3Upload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			professionalExperiences.Emp3COEUploadFile = ms.ToArray();
			professionalExperiences.Emp3COEUploadFileName = e.File.Name;
		}

		return;
	}

	private void OnSameAsPermanentChanged(bool value)
	{
		SameAsPermanent = value;

		if (SameAsPermanent)
		{
			addressDetails.PermanentAddress = addressDetails.CurrentAddress;
			addressDetails.PermanentCity = addressDetails.CurrentCity;
			addressDetails.PermanentProvince = addressDetails.CurrentProvince;
			addressDetails.PermanentCountry = addressDetails.CurrentCountry;
			addressDetails.PermanentPostalCode = addressDetails.CurrentPostalCode;
		}
		else
		{
			addressDetails.PermanentAddress = string.Empty;
			addressDetails.PermanentCity = string.Empty;
			addressDetails.PermanentProvince = string.Empty;
			addressDetails.PermanentCountry = string.Empty;
			addressDetails.PermanentPostalCode = string.Empty;
		}
	}

	private void NoMiddleNameChange(bool value)
	{
		NoMiddleName = value;

		if (NoMiddleName)
		{
			personalDetails.MiddleName = string.Empty;
		}
	}

	private bool DisableEducationFieldsForAboveCollege()
	{
		if (string.IsNullOrEmpty(educationalBackground.HighestEducationalAttainment) ||
			educationalBackground.HighestEducationalAttainment == "None" ||
			educationalBackground.HighestEducationalAttainment  == "Elementary Graduate")
		{
			return true;
		}
		return false;
	}

	private bool DisableEducationForBelowCollege()
	{
		if (string.IsNullOrEmpty(educationalBackground.HighestEducationalAttainment) ||
			educationalBackground.HighestEducationalAttainment == "None" || 
			educationalBackground.HighestEducationalAttainment == "Elementary Graduate" ||
			educationalBackground.HighestEducationalAttainment == "Junior High School Graduate" ||
			educationalBackground.HighestEducationalAttainment == "Senior High School Graduate")
		{
			return true;
		}
		return false;
	}

	private async Task OnSaveAndNextAsync()
	{
		await ApplicationForm!.ValidateAsync();
		if (ApplicationForm.IsValid)
		{
			if (_stepper is not null)
				await _stepper.NextStepAsync();
		}
	}

	private Task OnSignatureChanged(byte[]? value)
	{
		signatureDetails.Signature = value;

		_signatureError = value == null || value.Length == 0;

		return Task.CompletedTask;
	}

	private async Task OnSubmitForm()
	{
		await ApplicationForm!.ValidateAsync();

		_signatureError = signatureDetails.Signature == null ||
					 signatureDetails.Signature.Length == 0;

		if (!ApplicationForm.IsValid || _signatureError)
		{
			await InvokeAsync(StateHasChanged);
			return;
		}

		personalDetails.EmailInvitationID = EmailId;
		addressDetails.EmailInvitationID = EmailId;
		educationalBackground.EmailInvitationID = EmailId;
		licensesDetails.EmailInvitationID = EmailId;
		professionalExperiences.EmailInvitationID = EmailId;
		referenceDetails.EmailInvitationID = EmailId;
		signatureDetails.EmailInvitationID = EmailId;

		personalDetails.DOB = DateOnly.FromDateTime(DateOfBirth!.Value);
		signatureDetails.SignatureDate = DateOnly.FromDateTime(SignatureDate!.Value);

		if (NoMiddleName && string.IsNullOrEmpty(personalDetails.MiddleName))
		{
			personalDetails.MiddleName = string.Empty;
		}

		if (educationalBackground!.HighestEducationalAttainment!.Contains("Junior High School Graduate"))
		{
			educationalBackground.HighSchoolGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
			educationalBackground.HighSchoolDiplomaFile = educationalBackground.DiplomaFile;
			educationalBackground.HighSchoolDiplomaFileName = educationalBackground.DiplomaFileName;
			educationalBackground.HighSchoolName = AcademicInstitution;
		}
		else if (educationalBackground!.HighestEducationalAttainment!.Contains("Senior High School Graduate"))
		{
			educationalBackground.SeniorHighSchoolGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
			educationalBackground.SeniorHighSchoolDiplomaFile = educationalBackground.DiplomaFile;
			educationalBackground.SeniorHighSchoolDiplomaFileName = educationalBackground.DiplomaFileName;
			educationalBackground.SeniorHighSchoolName = AcademicInstitution;
		}
		else if (educationalBackground!.HighestEducationalAttainment!.Contains("College Graduate"))
		{
			educationalBackground.BachelorsGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
			educationalBackground.BachelorsDegree = DegreeWithMajor;
			educationalBackground.BachelorsDiplomaFile = educationalBackground.DiplomaFile;
			educationalBackground.BachelorsDiplomaFileName = educationalBackground.DiplomaFileName;
			educationalBackground.BachelorsSchoolName = AcademicInstitution;
		}
		else if (educationalBackground!.HighestEducationalAttainment!.Contains("Master's Graduate"))
		{
			educationalBackground.MastersGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
			educationalBackground.MastersDegree = DegreeWithMajor;
			educationalBackground.MastersDiplomaFile = educationalBackground.DiplomaFile;
			educationalBackground.MastersDiplomaFileName = educationalBackground.DiplomaFileName;
			educationalBackground.MastersSchoolName = AcademicInstitution;
		}
		else if (educationalBackground!.HighestEducationalAttainment!.Contains("Doctorate Graduate"))
		{
			educationalBackground.DoctorateGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
			educationalBackground.DoctorateDegree = DegreeWithMajor;
			educationalBackground.DoctorateDiplomaFile = educationalBackground.DiplomaFile;
			educationalBackground.DoctorateDiplomaFileName = educationalBackground.DiplomaFileName;
			educationalBackground.PhDSchoolName = AcademicInstitution;
		}

		if (!string.IsNullOrEmpty(OwnershipOtherText))
		{
			addressDetails.TypeOfOwnership = OwnershipOtherText;
		}

		if (hasProfessionalLicense && LicenseExpiryDate.HasValue)
		{
			licensesDetails.LicenseExpiryDate =
				DateOnly.FromDateTime(LicenseExpiryDate.Value);
		}

		professionalExperiences.Emp1DatePermittedToContact = DateOnly.FromDateTime(DatePermittedToContact1!.Value);
		professionalExperiences.Emp1StartDate = DateOnly.FromDateTime(StartOfEmployment1!.Value);
		professionalExperiences.Emp1EndDate = DateOnly.FromDateTime(EndOfEmployment1!.Value);

		if (DatePermittedToContact2.HasValue && StartOfEmployment2.HasValue && EndOfEmployment2.HasValue)
		{
			professionalExperiences.Emp2DatePermittedToContact = DateOnly.FromDateTime(DatePermittedToContact2.Value);
			professionalExperiences.Emp2StartDate = DateOnly.FromDateTime(StartOfEmployment2.Value);
			professionalExperiences.Emp2EndDate = DateOnly.FromDateTime(EndOfEmployment2.Value);
		}

		if (DatePermittedToContact3.HasValue && StartOfEmployment3.HasValue && EndOfEmployment3.HasValue)
		{
			professionalExperiences.Emp3DatePermittedToContact = DateOnly.FromDateTime(DatePermittedToContact3!.Value);
			professionalExperiences.Emp3StartDate = DateOnly.FromDateTime(StartOfEmployment3!.Value);
			professionalExperiences.Emp3EndDate = DateOnly.FromDateTime(EndOfEmployment3!.Value);
		}

		if (Ref1BestDate.HasValue && Ref1BestTime.HasValue)
		{
			referenceDetails.Ref1BestTimeToContact = DateTime.SpecifyKind(Ref1BestDate.Value.Date + Ref1BestTime.Value, DateTimeKind.Utc);
		}
		if (Ref2BestDate.HasValue && Ref2BestTime.HasValue)
		{
			referenceDetails.Ref2BestTimeToContact = DateTime.SpecifyKind(Ref2BestDate.Value.Date + Ref2BestTime.Value, DateTimeKind.Utc);
		}
		if (Ref3BestDate.HasValue && Ref3BestTime.HasValue)
		{
			referenceDetails.Ref3BestTimeToContact = DateTime.SpecifyKind(Ref3BestDate.Value.Date + Ref3BestTime.Value, DateTimeKind.Utc);
		}

		try
		{
			isSaving = true;
			await InvokeAsync(StateHasChanged);
			await ATSService.AddApplicationFormDataAsync(personalDetails, addressDetails, educationalBackground, licensesDetails, professionalExperiences, referenceDetails, signatureDetails);
			IsSuccess = true;
		}
		finally
		{
			await LocalStorageService.RemoveItemAsync($"{HashToken}_firstName");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_middleName");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_lastName");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_suffix");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_birthDate");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_sex");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_emailAddress");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_phoneNumber");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_profilePicture");
			isSaving = false;
		}
	}
}
