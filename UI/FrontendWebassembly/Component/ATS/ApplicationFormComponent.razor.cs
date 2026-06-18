namespace FrontendWebassembly.Component.ATS;

public partial class ApplicationFormComponent
{

	private MudForm _form;
	[Parameter]
	public bool ShowsPhilSys { get; set; } = false;
	[Parameter]
	public int ActiveStep { get; set; } = 1;
	[Parameter]
	public string? HashToken { get; set; }
	private string? FaceUrl;
	private bool IsSuccess = false;
	private static readonly Guid FixedGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

	// Stepper and general
	private MudStepper? _stepper;
	private int _activeStep;
	private bool showPhilSys = false;

	private void ClearSignature()
	{
		signatureDetails.Signature = null;
	}

	// Personal Detials
	private bool consent { get; set; } = false;
	private bool declineConsent = false;
	private PersonalDetailsDTO personalDetails = new();
	private bool NoMiddleName = false;
	private DateTime? DateOfBirth;
	private IBrowserFile? AdditionalGovtIDFile;
	private IBrowserFile? NBIClearanceFile;
	private IBrowserFile? ResumeFile;
	private MudFileUpload<IBrowserFile> AdditionalGovtIDFileUpload = default!;
	private MudFileUpload<IBrowserFile> NBIClearanceFileUpload = default!;
	private MudFileUpload<IBrowserFile> ResumeFileUpload = default!;
	private string? CVOrResumeFileName;

	// AddressDetials
	private AddressDetailsDTO addressDetails = new();
	private bool SameAsPermanent;
	private string? OwnershipOtherText;

	// Educational background
	private EducationalBackgroundDTO educationalBackground = new();
	private string? HighestEducationalAttainment;
	private DateTime? GraduationDate;
	private string? DegreeWithMajor;
	private string? AcademicInstitution;
	private IBrowserFile? DiplomaORTORFile;
	private string? DiplomaORTORFileName;
	private MudFileUpload<IBrowserFile> DiplomaORTORUploadFile = default!;

	// Step 4 - credentials & experience
	private LicensesDetailsDTO licensesDetails = new();
	private DateTime? LicenseExpiryDate;
	private IBrowserFile? LicenseFile;
	private MudFileUpload<IBrowserFile> LicenseUploadFile = default!;

	private ProfessionalExperiencesDTO professionalExperiences = new();
	private DateTime? DatePermittedToContact;
	private DateTime? StartOfEmployment;
	private DateTime? EndOfEmployment;
	private bool AddAnotherEmployer;
	private IBrowserFile? Emp1COEFile;
	private MudFileUpload<IBrowserFile> Emp1COEFileUpload = default!;

	// Step 5 - references
	private ReferenceDetailsDTO referenceDetails = new();
	private DateTime? Ref1BestDate;
	private TimeSpan? Ref1BestTime;
	private DateTime? Ref2BestDate;
	private TimeSpan? Ref2BestTime;
	private bool AddAnotherReference;

	//Final
	private SignatureDetailsDTO signatureDetails = new();
	private DateTime? SignatureDate = DateTime.UtcNow;


	private SignaturePadOptions _options = new SignaturePadOptions
	{
		LineCap = LineCap.Round,
		LineJoin = LineJoin.Round,
		LineWidth = 20
	};

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

	private async Task RemoveFileFromUploadsAsync(IBrowserFile file)
	{
		if (await AdditionalGovtIDFileUpload.RemoveFileAsync(file))
		{
			LicenseFile = null;
			return;
		}
		if (await NBIClearanceFileUpload.RemoveFileAsync(file))
		{
			NBIClearanceFile = null;
			return;
		}
		if (await ResumeFileUpload.RemoveFileAsync(file))
		{
			ResumeFile = null;
			return;
		}
		if (await DiplomaORTORUploadFile.RemoveFileAsync(file))
		{
			DiplomaORTORFile = null;
			return;
		}
		if (await LicenseUploadFile.RemoveFileAsync(file))
		{
			LicenseFile = null;
			return;
		}
		if (await Emp1COEFileUpload.RemoveFileAsync(file))
		{
			Emp1COEFile = null;
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

	private async Task PreviousStepper()
	{
		if (_stepper is not null)
			await _stepper.PreviousStepAsync();
	}

	private async Task ProceedClicked()
	{
		showPhilSys = true;
	}

	private async Task OnGovtIdUpload(InputFileChangeEventArgs e)
	{
		AdditionalGovtIDFile = e.File;
		personalDetails.AdditionalGovtIDFileName = e.File.Name;


		if (AdditionalGovtIDFile != null)
		{
			using var ms = new MemoryStream();

			await AdditionalGovtIDFile
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.ResumeFile = ms.ToArray();
		}

		return;
	}

	private async Task OnNbiUpload(InputFileChangeEventArgs e)
	{
		NBIClearanceFile = e.File;
		personalDetails.NBIClearanceFileName = e.File.Name;

		if (NBIClearanceFile != null)
		{
			using var ms = new MemoryStream();

			await NBIClearanceFile
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.NBIClearanceFile = ms.ToArray();
		}
		return;
	}

	private async Task OnCvUpload(InputFileChangeEventArgs e)
	{
		ResumeFile = e.File;
		personalDetails.ResumeFileName = e.File.Name;
		if (ResumeFile != null)
		{
			using var ms = new MemoryStream();

			await ResumeFile
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.ResumeFile = ms.ToArray();
		}
		return;
	}

	private async Task OnDiplomaUpload(InputFileChangeEventArgs e)
	{
		DiplomaORTORFile = e.File;
		DiplomaORTORFileName = e.File.Name;
		if (DiplomaORTORFile is not null)
		{
			using var ms = new MemoryStream();

			await DiplomaORTORFile
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			if (educationalBackground!.HighestEducationalAttainment!.Contains("HighSchool Graduate"))
			{
				educationalBackground.HighSchoolGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
				educationalBackground.HighSchoolDiplomaFile = ms.ToArray();
			}
			else if (educationalBackground!.HighestEducationalAttainment!.Contains("Senior High School Graduate"))
			{
				educationalBackground.SeniorHighSchoolGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
				educationalBackground.SeniorHighSchoolDiplomaFile = ms.ToArray();
			}
			else if (educationalBackground!.HighestEducationalAttainment!.Contains("Bachelor's Degree"))
			{
				educationalBackground.BachelorsGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
				educationalBackground.BachelorsDegree = DegreeWithMajor;
				educationalBackground.BachelorsDiplomaFile = ms.ToArray();
			}
			else if (educationalBackground!.HighestEducationalAttainment!.Contains("Master's Degree"))
			{
				educationalBackground.MastersGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
				educationalBackground.MastersDegree = DegreeWithMajor;
				educationalBackground.MastersDiplomaFile = ms.ToArray();
			}
			else if (educationalBackground!.HighestEducationalAttainment!.Contains("Doctorate Degree"))
			{
				educationalBackground.DoctorateGraduationDate = DateOnly.FromDateTime(GraduationDate!.Value);
				educationalBackground.DoctorateDegree = DegreeWithMajor;
				educationalBackground.DoctorateDiplomaFile = ms.ToArray();
			}
		}
		return;
	}

	private async Task OnProfessionalLicenseUpload(InputFileChangeEventArgs e)
	{
		LicenseFile = e.File;
		licensesDetails.LicenseUploadFileName = e.File.Name;
		if (LicenseFile != null)
		{
			using var ms = new MemoryStream();

			await LicenseFile
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			licensesDetails.LicenseUploadFile = ms.ToArray();
		}
		return;
	}

	private async Task OnCoeUpload(InputFileChangeEventArgs e)
	{
		Emp1COEFile = e.File;
		professionalExperiences.Emp1COEUploadFileName = e.File.Name;
		if (Emp1COEFile != null)
		{
			using var ms = new MemoryStream();

			await Emp1COEFile
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			professionalExperiences.Emp1COEUploadFile = ms.ToArray();
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

	private string ValidateEmailAlternative(string value)
	{
		if (!value.Contains("@"))
			return "Invalid email format";

		var regex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
		if (!System.Text.RegularExpressions.Regex.IsMatch(value, regex))
			return "Invalid email format";

		return null!;
	}


	private async Task OnSaveAndNextAsync()
	{
		await _form.ValidateAsync();
		if (_form.IsValid)
		{
			if (_stepper is not null)
				await _stepper.NextStepAsync();
		}
	}

	private async Task OnSubmitForm()
	{
		personalDetails.EmailInvitationID = FixedGuid;
		addressDetails.EmailInvitationID = FixedGuid;
		educationalBackground.EmailInvitationID = FixedGuid;
		licensesDetails.EmailInvitationID = FixedGuid;
		professionalExperiences.EmailInvitationID = FixedGuid;
		referenceDetails.EmailInvitationID = FixedGuid;
		signatureDetails.EmailInvitationID = FixedGuid;

		personalDetails.DOB = DateOnly.FromDateTime(DateOfBirth!.Value);
		signatureDetails.SignatureDate = DateOnly.FromDateTime(SignatureDate!.Value);

		if (NoMiddleName && string.IsNullOrEmpty(personalDetails.MiddleName))
		{
			personalDetails.MiddleName = string.Empty;
		}

		addressDetails.TypeOfOwnership = OwnershipOtherText;

		licensesDetails.LicenseExpiryDate = DateOnly.FromDateTime(LicenseExpiryDate!.Value);

		professionalExperiences.Emp1DatePermittedToContact = DateOnly.FromDateTime(DatePermittedToContact!.Value);
		professionalExperiences.Emp1StartDate = DateOnly.FromDateTime(StartOfEmployment!.Value);
		professionalExperiences.Emp1EndDate = DateOnly.FromDateTime(EndOfEmployment!.Value);

		if (Ref1BestDate.HasValue && Ref1BestTime.HasValue)
		{
			referenceDetails.Ref1BestTimeToContact = DateTime.SpecifyKind(Ref1BestDate.Value.Date + Ref1BestTime.Value, DateTimeKind.Utc);
		}
		if (Ref2BestDate.HasValue && Ref2BestTime.HasValue)
		{
			referenceDetails.Ref2BestTimeToContact = DateTime.SpecifyKind(Ref2BestDate.Value.Date + Ref2BestTime.Value, DateTimeKind.Utc);
		}

		try
		{
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
		}
	}
}
