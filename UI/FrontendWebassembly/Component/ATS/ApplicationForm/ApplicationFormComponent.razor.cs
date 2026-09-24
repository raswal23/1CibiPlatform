namespace FrontendWebassembly.Component.ATS;

public partial class ApplicationFormComponent
{
	private MudForm? ApplicationForm;
	[Parameter]
	public Guid EmailId { get; set; }
	[Parameter]
	public bool ShowsPhilSys { get; set; } = false;
	[Parameter]
	public int ActiveStep { get; set; } = 0;
	[Parameter]
	public string? HashToken { get; set; }
	// Birth date the requestor supplied at order entry (data-screening orders);
	// pre-fills the form so the candidate need not retype it.
	[Parameter]
	public DateOnly? OrderDateOfBirth { get; set; }
	private string? FaceUrl;
	private bool IsSuccess = false;
	private bool hasProfessionalLicense = false;
	private string? philSysId;

	// Stepper and general
	private MudStepper? _stepper;
	private int _activeStep;
	private bool showPhilSys = false;
	private bool isSaving = false;

	// Personal Details
	private bool consent { get; set; } = false;
	private bool declineConsent = false;
	private string? ConsentChoice
	{
		get => consent ? "yes" : declineConsent ? "no" : null;
		set
		{
			consent = value == "yes";
			declineConsent = value == "no";
		}
	}
	private PersonalDetailsDTO personalDetails = new();
	private bool NoMiddleName = false;
	private MudTextField<string>? _middleNameField;
	private DateTime? DateOfBirth;

	// AddressDetails
	private const string OtherOwnershipType = "Others";
	private AddressDetailsDTO addressDetails = new();
	private bool SameAsPermanent;
	private string? OwnershipOtherText = null;
	private string? SelectedOwnershipType;

	private bool IsOtherOwnershipSelected =>
		string.Equals(SelectedOwnershipType, OtherOwnershipType, StringComparison.Ordinal);

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
	private bool AddEmployer2 = false;
	private bool AddEmployer3 = false;
	private bool hasWorkExperience = false;

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
	private DateTime? SignatureDate;
	private bool _signatureError;

	//Validations
	private bool _govtIdError;
	private bool _resumeError;
	private bool _nbiError;

	private bool _emp1Error;
	private bool _emp2Error;
	private bool _emp3Error;

	private bool _licenseError;
	private bool _diplomaError;

	protected override async Task OnInitializedAsync()
	{
		await RestoreDraftAsync();

		philSysId = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:digitalId") ?? string.Empty;
		personalDetails.FirstName = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:firstName") ?? personalDetails.FirstName;
		personalDetails.MiddleName = NoMiddleName
			? string.Empty
			: await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:middleName") ?? personalDetails.MiddleName;
		personalDetails.LastName = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:lastName") ?? personalDetails.LastName;
		personalDetails.Suffix = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:suffix") ?? personalDetails.Suffix;
		string? dobString = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:birthDate");
		personalDetails.Sex = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:sex") ?? personalDetails.Sex;
		personalDetails.EmailAlternative = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:emailAddress") ?? personalDetails.EmailAlternative;
		personalDetails.MobileNumber = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:phoneNumber") ?? personalDetails.MobileNumber;
		FaceUrl = await LocalStorageService.GetItemAsync<string?>($"ats:applicationForm:profilePicture") ?? string.Empty;

		if (!string.IsNullOrEmpty(FaceUrl))
		{
			var uri = new Uri(FaceUrl);

			personalDetails.BiometricFileName = Path.GetFileName(uri.AbsolutePath);

			try
			{
				personalDetails.BiometricFile = await Http.GetByteArrayAsync(FaceUrl);
			}
			catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
			{
				await LocalStorageService.RemoveItemAsync($"ats:applicationForm:profilePicture");
				FaceUrl = string.Empty;
				personalDetails.BiometricFileName = null;
				personalDetails.BiometricFile = null;
			}
		}

		showPhilSys = ShowsPhilSys;

		if (!string.IsNullOrWhiteSpace(dobString))
		{
			if (DateOnly.TryParseExact(dobString, "yyyy-MM-dd", out var dob))
			{
				DateOfBirth = dob.ToDateTime(TimeOnly.MinValue);
			}
		}

		// A birth date captured at order entry pre-fills the picker. The PhilSys
		// value (above) wins when both exist - it came from a verified identity,
		// and the candidate can still correct the field either way.
		if (DateOfBirth is null && OrderDateOfBirth is { } orderDob)
		{
			DateOfBirth = orderDob.ToDateTime(TimeOnly.MinValue);
		}

		SignatureDate = DateTime.UtcNow;

		_activeStep = Math.Clamp(ActiveStep, 0, 5);
		_draftPersistenceEnabled = true;
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await JS.InvokeVoidAsync("general.attachNameFilter");
	}

	private bool CanAddEmployer2 =>
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1CompanyName) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1CompanyCity) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1CompanyProvince) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1CompanyCountry) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1CompanyPostalCode) &&
		DatePermittedToContact1.HasValue &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1JobTitle) &&
		StartOfEmployment1.HasValue &&
		(professionalExperiences.Emp1CurrentlyEmployed || EndOfEmployment1.HasValue) &&
		professionalExperiences.Emp1COEUploadFile is not null &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1SupervisorName) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1SupervisorEmail) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp1SupervisorContactNumber);

	private bool CanAddEmployer3 =>
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2CompanyName) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2CompanyCity) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2CompanyProvince) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2CompanyCountry) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2CompanyPostalCode) &&
		DatePermittedToContact2.HasValue &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2JobTitle) &&
		StartOfEmployment2.HasValue &&
		(professionalExperiences.Emp2CurrentlyEmployed || EndOfEmployment2.HasValue) &&
		professionalExperiences.Emp2COEUploadFile is not null &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2SupervisorName) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2SupervisorEmail) &&
		!string.IsNullOrWhiteSpace(professionalExperiences.Emp2SupervisorContactNumber);

	private async Task RemoveFileFromUploadsAsync(byte[] file)
	{
		// Personal Details
		if (file == personalDetails.AdditionalGovtIDFile)
		{
			personalDetails.AdditionalGovtIDFile = null;
			personalDetails.AdditionalGovtIDFileName = null;
			return;
		}

		if (file == personalDetails.NBIClearanceFile)
		{
			personalDetails.NBIClearanceFile = null;
			personalDetails.NBIClearanceFileName = null;
			return;
		}

		if (file == personalDetails.ResumeFile)
		{
			personalDetails.ResumeFile = null;
			personalDetails.ResumeFileName = null;
			return;
		}

		// Educational Background
		if (file == educationalBackground.DiplomaFile)
		{
			educationalBackground.DiplomaFile = null;
			educationalBackground.DiplomaFileName = null;
			return;
		}

		// Licenses
		if (file == licensesDetails.LicenseUploadFile)
		{
			licensesDetails.LicenseUploadFile = null;
			licensesDetails.LicenseUploadFileName = null;
			return;
		}

		// Experience
		if (file == professionalExperiences.Emp1COEUploadFile)
		{
			professionalExperiences.Emp1COEUploadFile = null;
			professionalExperiences.Emp1COEUploadFileName = null;
			return;
		}

		if (file == professionalExperiences.Emp2COEUploadFile)
		{
			professionalExperiences.Emp2COEUploadFile = null;
			professionalExperiences.Emp2COEUploadFileName = null;
			return;
		}

		if (file == professionalExperiences.Emp3COEUploadFile)
		{
			professionalExperiences.Emp3COEUploadFile = null;
			professionalExperiences.Emp3COEUploadFileName = null;
			return;
		}

	}

	private Task OnAddEmployer3Changed(bool value)
	{
		if (!value)
		{
			RemoveEmployer3();
			return Task.CompletedTask;
		}

		if (!CanAddEmployer3)
		{
			Snackbar.Add("Please complete Employer 2 before adding Employer 3.", Severity.Warning);
			return Task.CompletedTask;
		}

		// Matches Employer 1's default. RemoveEmployer3 clears the flag along with the rest
		// of the card, so revealing it again is what re-ticks the box.
		professionalExperiences.Emp3PermissionToContact = true;
		AddEmployer3 = true;
		return Task.CompletedTask;
	}

	private Task OnAddEmployer2Changed(bool value)
	{
		if (!value)
		{
			RemoveEmployer2();
			return Task.CompletedTask;
		}

		if (!CanAddEmployer2)
		{
			Snackbar.Add("Please complete Employer 1 before adding Employer 2.", Severity.Warning);
			return Task.CompletedTask;
		}

		// Matches Employer 1's default. RemoveEmployer2 clears the flag along with the rest
		// of the card, so revealing it again is what re-ticks the box.
		professionalExperiences.Emp2PermissionToContact = true;
		AddEmployer2 = true;
		return Task.CompletedTask;
	}

	// Removing an employer clears its fields, it does not just hide them. Collapsing the card
	// while the DTO kept its values meant a candidate who added Employer 2, filled it in, then
	// removed it still submitted that employer - invisibly, with no way to review or correct it
	// on the form they were looking at. Same reasoning as ClearProfessionalExperienceDetails,
	// which already does this for "no work experience".
	//
	// The paired date fields are separate because the pickers bind DateTime? while the DTO
	// holds DateOnly?; leaving them set would repopulate the slot on the next SaveDraft.
	private void RemoveEmployer2()
	{
		// Employer 3 goes first: the form only offers it once Employer 2 exists, so leaving a
		// filled Employer 3 behind a removed Employer 2 would submit a record the candidate
		// cannot see or reach.
		RemoveEmployer3();

		professionalExperiences.Emp2CompanyName = null;
		professionalExperiences.Emp2CurrentlyEmployed = false;
		professionalExperiences.Emp2PermissionToContact = false;
		professionalExperiences.Emp2CompanyCity = null;
		professionalExperiences.Emp2CompanyProvince = null;
		professionalExperiences.Emp2CompanyCountry = null;
		professionalExperiences.Emp2CompanyPostalCode = null;
		professionalExperiences.Emp2StartDate = null;
		professionalExperiences.Emp2EndDate = null;
		professionalExperiences.Emp2DatePermittedToContact = null;
		professionalExperiences.Emp2JobTitle = null;
		professionalExperiences.Emp2SupervisorName = null;
		professionalExperiences.Emp2SupervisorEmail = null;
		professionalExperiences.Emp2SupervisorContactNumber = null;
		professionalExperiences.Emp2COEUploadFile = null;
		professionalExperiences.Emp2COEUploadFileName = null;

		DatePermittedToContact2 = null;
		StartOfEmployment2 = null;
		EndOfEmployment2 = null;
		_emp2Error = false;

		AddEmployer2 = false;
	}

	private void RemoveEmployer3()
	{
		professionalExperiences.Emp3CompanyName = null;
		professionalExperiences.Emp3CurrentlyEmployed = false;
		professionalExperiences.Emp3PermissionToContact = false;
		professionalExperiences.Emp3CompanyCity = null;
		professionalExperiences.Emp3CompanyProvince = null;
		professionalExperiences.Emp3CompanyCountry = null;
		professionalExperiences.Emp3CompanyPostalCode = null;
		professionalExperiences.Emp3StartDate = null;
		professionalExperiences.Emp3EndDate = null;
		professionalExperiences.Emp3DatePermittedToContact = null;
		professionalExperiences.Emp3JobTitle = null;
		professionalExperiences.Emp3SupervisorName = null;
		professionalExperiences.Emp3SupervisorEmail = null;
		professionalExperiences.Emp3SupervisorContactNumber = null;
		professionalExperiences.Emp3COEUploadFile = null;
		professionalExperiences.Emp3COEUploadFileName = null;

		DatePermittedToContact3 = null;
		StartOfEmployment3 = null;
		EndOfEmployment3 = null;
		_emp3Error = false;

		AddEmployer3 = false;
	}

	// Reference 3 is the only removable reference - 1 and 2 are always required.
	private void RemoveReference3()
	{
		referenceDetails.Ref3FullName = null;
		referenceDetails.Ref3ProfessionalRelationship = null;
		referenceDetails.Ref3AffiliatedCompany = null;
		referenceDetails.Ref3Email = null;
		referenceDetails.Ref3ContactNumber = null;
		referenceDetails.Ref3ModeOfContact = null;
		referenceDetails.Ref3BestTimeToContact = null;

		Ref3BestDate = null;
		Ref3BestTime = null;

		AddAnotherReference = false;
	}

	private void OnAddReference3Changed(bool value)
	{
		if (!value)
		{
			RemoveReference3();
			return;
		}

		AddAnotherReference = true;
	}

	private bool ValidateUploads()
	{
		return _activeStep switch
		{
			2 => !(
				(_govtIdError = personalDetails.AdditionalGovtIDFile == null) |
				(_resumeError = personalDetails.ResumeFile == null) |
				(_nbiError = personalDetails.NBIClearanceFile == null)
			),

			3 => !(

				(_diplomaError = educationalBackground.DiplomaFile == null
								&& !string.IsNullOrEmpty(HighestEducationalAttainment)
								&& educationalBackground.HighestEducationalAttainment != "None"
								&& educationalBackground.HighestEducationalAttainment != "Elementary Graduate")
			),

			4 => !(
				(_licenseError = licensesDetails.LicenseUploadFile == null
								&& hasProfessionalLicense) |
				(_emp1Error = professionalExperiences.Emp1COEUploadFile == null
								&& hasWorkExperience) |
				(_emp2Error = professionalExperiences.Emp2COEUploadFile == null && AddEmployer2) |
				(_emp3Error = professionalExperiences.Emp3COEUploadFile == null && AddEmployer3)
			),

			_ => true
		}; ;
	}

	private async Task SkipStep()
	{
		if (_stepper is not null)
		{
			await _stepper.SkipCurrentStepAsync();
			await SaveDraftAsync();
		}
	}

	private async Task OnPreviousStepAsync()
	{
		if (_stepper is not null)
		{
			await _stepper.PreviousStepAsync();
			await SaveDraftAsync();
		}
	}

	private async Task CancelTransaction()
	{
		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Withdraw Application"
			},
			{
				nameof(YesNoDialogComponent.Message),
				"Please be advised that this action will withdraw the application form."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Withdraw"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"By clicking 'Withdraw', the application form will be withdrawn and you will not be able to submit it."
			},
			{
				nameof(YesNoDialogComponent.ConfirmActionAsync),
				(Func<Task<bool>>)WithdrawApplicationFormAsync
			},
			{
				nameof(YesNoDialogComponent.AvatarIcon),Icons.Material.Filled.WarningAmber
			},
			{
				nameof(YesNoDialogComponent.AvatarColor),Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoColor),Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoBGColor),"var(--c-warn-bg)"
			},
			{
				nameof(YesNoDialogComponent.ThemeButtonColor),"theme-button-warning"
			}

		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.ExtraSmall,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<YesNoDialogComponent>(null, confirmParam, options);

		await dialog.Result;
	}

	// The dialog owns the wait: it runs this on Withdraw, spins its confirm button for the
	// duration, and only closes when this returns true. Returning false on a failed withdraw
	// keeps the dialog open so the candidate can read the snackbar and retry, rather than
	// dropping them back on a form that silently did nothing.
	private async Task<bool> WithdrawApplicationFormAsync()
	{
		var withdrawResponse = await ATSService.WithdrawApplicationForm(HashToken!);

		if (!withdrawResponse.IsSuccess)
		{
			Snackbar.Add(withdrawResponse.ErrorDetail, Severity.Error);
			return false;
		}

		if (!withdrawResponse.Data)
		{
			Snackbar.Add("Failed to withdraw the application form.", Severity.Error);
			return false;
		}

		await IsWithDrawn.InvokeAsync("Withdrawn");
		await RemoveItemsAsync();

		return true;
	}

	private async Task ProceedClicked()
	{
		showPhilSys = true;
	}

	[Parameter] public EventCallback<bool> HasChangesChanged { get; set; }
	[Parameter] public EventCallback<string> IsWithDrawn { get; set; }

	private async Task OnChanged()
	{
		await HasChangesChanged.InvokeAsync(false);
	}

	private bool ValidateUploadFile(string fileName)
	{
		var result = FileValidationService.ValidateExtension(fileName, ".pdf");

		if (!result.IsValid)
		{
			Snackbar.Add(result.ErrorMessage!, Severity.Error);
			return false;
		}

		return true;
	}

	private async Task OnGovtIdUpload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			var isValid = ValidateUploadFile(e.File.Name);
			if (!isValid)
				return;

			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.AdditionalGovtIDFile = ms.ToArray();
			personalDetails.AdditionalGovtIDFileName = e.File!.Name;
		}

		_govtIdError = false;

		return;
	}

	private async Task OnNbiUpload(InputFileChangeEventArgs e)
	{
		if (e.File != null)
		{
			var isValid = ValidateUploadFile(e.File.Name);
			if (!isValid)
				return;

			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.NBIClearanceFile = ms.ToArray();
			personalDetails.NBIClearanceFileName = e.File.Name;
		}

		_nbiError = false;
		return;
	}

	private async Task OnCvUpload(InputFileChangeEventArgs e)
	{
		var isValid = ValidateUploadFile(e.File.Name);
		if (!isValid)
			return;

		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			personalDetails.ResumeFile = ms.ToArray();
			personalDetails.ResumeFileName = e.File.Name;
		}

		_resumeError = false;
		return;
	}

	private async Task OnDiplomaUpload(InputFileChangeEventArgs e)
	{
		var isValid = ValidateUploadFile(e.File.Name);
		if (!isValid)
			return;

		if (e.File is not null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			educationalBackground.DiplomaFile = ms.ToArray();
			educationalBackground.DiplomaFileName = e.File!.Name;
		}

		_diplomaError = false;
		return;
	}

	private async Task OnProfessionalLicenseUpload(InputFileChangeEventArgs e)
	{
		var isValid = ValidateUploadFile(e.File.Name);
		if (!isValid)
			return;

		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			licensesDetails.LicenseUploadFile = ms.ToArray();
			licensesDetails.LicenseUploadFileName = e.File.Name;
		}

		if (!hasProfessionalLicense)
		{
			ClearProfessionalLicenseDetails();
			return;
		}

		_licenseError = false;
		return;
	}

	private async Task SetProfessionalLicenseAsync(bool value)
	{
		hasProfessionalLicense = value;

		if (!value)
			ClearProfessionalLicenseDetails();

		await SaveDraftAsync();
	}

	private void ClearProfessionalLicenseDetails()
	{
		licensesDetails.LicenseName = null;
		licensesDetails.LicenseNumber = null;
		licensesDetails.LicenseExpiryDate = null;
		licensesDetails.LicenseUploadFile = null;
		licensesDetails.LicenseUploadFileName = null;
		LicenseExpiryDate = null;
		_licenseError = false;
	}

	private async Task SetWorkExperienceAsync(bool value)
	{
		hasWorkExperience = value;

		if (!value)
			ClearProfessionalExperienceDetails();

		await SaveDraftAsync();
	}

	private void ClearProfessionalExperienceDetails()
	{
		professionalExperiences = new();
		DatePermittedToContact1 = null;
		StartOfEmployment1 = null;
		EndOfEmployment1 = null;
		DatePermittedToContact2 = null;
		StartOfEmployment2 = null;
		EndOfEmployment2 = null;
		DatePermittedToContact3 = null;
		StartOfEmployment3 = null;
		EndOfEmployment3 = null;
		AddEmployer2 = false;
		AddEmployer3 = false;
		_emp1Error = false;
		_emp2Error = false;
		_emp3Error = false;
	}

	private async Task OnCoe1Upload(InputFileChangeEventArgs e)
	{
		var isValid = ValidateUploadFile(e.File.Name);
		if (!isValid)
			return;

		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			professionalExperiences.Emp1COEUploadFile = ms.ToArray();
			professionalExperiences.Emp1COEUploadFileName = e.File.Name;
		}

		_emp1Error = false;
		return;
	}

	private async Task OnCoe2Upload(InputFileChangeEventArgs e)
	{
		var isValid = ValidateUploadFile(e.File.Name);
		if (!isValid)
			return;

		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			professionalExperiences.Emp2COEUploadFile = ms.ToArray();
			professionalExperiences.Emp2COEUploadFileName = e.File.Name;
		}
		_emp2Error = false;
		return;
	}

	private async Task OnCoe3Upload(InputFileChangeEventArgs e)
	{
		var isValid = ValidateUploadFile(e.File.Name);
		if (!isValid)
			return;

		if (e.File != null)
		{
			using var ms = new MemoryStream();

			await e.File
				.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024)
				.CopyToAsync(ms);

			professionalExperiences.Emp3COEUploadFile = ms.ToArray();
			professionalExperiences.Emp3COEUploadFileName = e.File.Name;
		}
		_emp3Error = false;
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

	// While "same as permanent" is checked, the permanent fields are disabled, so the
	// copy taken at toggle time is the only thing keeping them in sync. These handlers
	// keep mirroring later edits to the current address into the permanent one.
	private void OnCurrentAddressChanged(string value)
	{
		addressDetails.CurrentAddress = value;

		if (SameAsPermanent)
			addressDetails.PermanentAddress = value;
	}

	private void OnCurrentCityChanged(string value)
	{
		addressDetails.CurrentCity = value;

		if (SameAsPermanent)
			addressDetails.PermanentCity = value;
	}

	private void OnCurrentProvinceChanged(string value)
	{
		addressDetails.CurrentProvince = value;

		if (SameAsPermanent)
			addressDetails.PermanentProvince = value;
	}

	private void OnCurrentCountryChanged(string value)
	{
		addressDetails.CurrentCountry = value;

		if (SameAsPermanent)
			addressDetails.PermanentCountry = value;
	}

	private void OnCurrentPostalCodeChanged(string value)
	{
		addressDetails.CurrentPostalCode = value;

		if (SameAsPermanent)
			addressDetails.PermanentPostalCode = value;
	}

	private async Task NoMiddleNameChange(bool value)
	{
		NoMiddleName = value;

		if (NoMiddleName)
		{
			personalDetails.MiddleName = string.Empty;

			if (_middleNameField is not null)
			{
				await _middleNameField.ClearAsync();
				await _middleNameField.ResetValidationAsync();
			}
		}
	}

	private bool DisableEducationFieldsForAboveCollege()
	{
		if (string.IsNullOrEmpty(educationalBackground.HighestEducationalAttainment) ||
			educationalBackground.HighestEducationalAttainment == "None" ||
			educationalBackground.HighestEducationalAttainment == "Elementary Graduate")
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
	private void ResetEducationalBackground(string? education)
	{
		switch (education)
		{
			case "None":
			case "Elementary Graduate":

				GraduationDate = null;
				DegreeWithMajor = null;
				AcademicInstitution = null;

				educationalBackground.DiplomaFile = null;
				educationalBackground.DiplomaFileName = null;

				_diplomaError = false;
				break;

			case "Junior High School Graduate":
			case "Senior High School Graduate":

				DegreeWithMajor = null;
				break;

			case "College Graduate":

				_diplomaError = false;
				break;

			case "Master's Graduate":
			case "Doctorate Graduate":
				break;

			default:
				break;
		}
	}

	private string? HighestEducationalAttainment
	{
		get => educationalBackground.HighestEducationalAttainment;
		set
		{
			if (educationalBackground.HighestEducationalAttainment == value)
				return;

			educationalBackground.HighestEducationalAttainment = value;

			ResetEducationalBackground(value);

		}
	}
	private async Task OnSaveAndNextAsync()
	{
		await ApplicationForm!.ValidateAsync();

		if (_activeStep == 0)
		{
			_signatureError = signatureDetails.Signature is null ||
						  signatureDetails.Signature.Length == 0;

			if (_signatureError)
			{
				await InvokeAsync(StateHasChanged);
				return;
			}
		}

		bool uploadsValid = ValidateUploads();

		if (ApplicationForm.IsValid && uploadsValid)
		{
			if (_stepper is not null)
				await _stepper.NextStepAsync();
		}

		await SaveDraftAsync();
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
			if (_signatureError)
			{
				_activeStep = 0;
				await SaveDraftAsync();
			}

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

		addressDetails.TypeOfOwnership = IsOtherOwnershipSelected ? OwnershipOtherText : SelectedOwnershipType;

		if (hasProfessionalLicense && LicenseExpiryDate.HasValue)
		{
			licensesDetails.LicenseExpiryDate =
				DateOnly.FromDateTime(LicenseExpiryDate.Value);
		}



		if (hasWorkExperience)
		{
			if (EndOfEmployment1 is null)
				EndOfEmployment1 = DateTime.UnixEpoch;

			if (EndOfEmployment2 is null)
				EndOfEmployment2 = DateTime.UnixEpoch;

			if (EndOfEmployment3 is null)
				EndOfEmployment3 = DateTime.UnixEpoch;

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
			var response = await ATSService.AddApplicationFormDataAsync(HashToken!, personalDetails, addressDetails, educationalBackground, licensesDetails, professionalExperiences, referenceDetails, signatureDetails);

			if (!response.IsSuccess)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}
			IsSuccess = response.IsSuccess;
			await RemoveItemsAsync();
		}
		finally
		{
			isSaving = false;
		}
	}

	private async Task RemoveItemsAsync()
	{
		await ClearDraftAsync();
		await OnChanged();
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:firstName");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:middleName");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:lastName");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:suffix");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:birthDate");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:sex");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:emailAddress");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:phoneNumber");
		await LocalStorageService.RemoveItemAsync($"ats:applicationForm:profilePicture");
	}
}
