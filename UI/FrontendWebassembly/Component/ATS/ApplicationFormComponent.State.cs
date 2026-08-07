namespace FrontendWebassembly.Component.ATS;

public partial class ApplicationFormComponent : IAsyncDisposable
{
	private static readonly TimeSpan DraftSaveDelay = TimeSpan.FromMilliseconds(700);
	private CancellationTokenSource? _draftSaveCancellation;
	private bool _draftPersistenceEnabled;
	private bool _draftCleared;

	private Task ScheduleDraftSaveFromField() => ScheduleDraftSaveAsync();

	private Task ScheduleDraftSaveFromDom(EventArgs _) => ScheduleDraftSaveAsync();

	private async Task ScheduleDraftSaveAsync()
	{
		if (!_draftPersistenceEnabled)
			return;

		_draftCleared = false;
		_draftSaveCancellation?.Cancel();
		_draftSaveCancellation?.Dispose();
		_draftSaveCancellation = new CancellationTokenSource();

		try
		{
			await Task.Delay(DraftSaveDelay, _draftSaveCancellation.Token);
			await SaveDraftAsync();
		}
		catch (OperationCanceledException)
		{
			// A newer form change restarted the debounce interval.
		}
	}

	private async Task SaveDraftAsync()
	{
		if (!_draftPersistenceEnabled || _draftCleared || IsSuccess)
			return;

		await ApplicationFormStateService.SaveAsync(CreateDraftState());
	}

	private ApplicationFormState CreateDraftState()
	{
		return new ApplicationFormState
		{
			EmailInvitationId = EmailId,
			ActiveStep = _activeStep,
			PersonalDetails = new PersonalDetailsState
			{
				PositionAppliedFor = personalDetails.PositionAppliedFor,
				FirstName = personalDetails.FirstName,
				MiddleName = personalDetails.MiddleName,
				LastName = personalDetails.LastName,
				Suffix = personalDetails.Suffix,
				Sex = personalDetails.Sex,
				DateOfBirth = DateOfBirth,
				MobileNumber = personalDetails.MobileNumber,
				EmailAlternative = personalDetails.EmailAlternative,
				NoMiddleName = NoMiddleName,
				AdditionalGovernmentId = FileMetadata(personalDetails.AdditionalGovtIDFileName, personalDetails.AdditionalGovtIDFile),
				NbiClearance = FileMetadata(personalDetails.NBIClearanceFileName, personalDetails.NBIClearanceFile),
				Resume = FileMetadata(personalDetails.ResumeFileName, personalDetails.ResumeFile),
				BiometricPhoto = FileMetadata(personalDetails.BiometricFileName, personalDetails.BiometricFile)
			},
			AddressDetails = new AddressDetailsState
			{
				CurrentAddress = addressDetails.CurrentAddress,
				CurrentCity = addressDetails.CurrentCity,
				CurrentProvince = addressDetails.CurrentProvince,
				CurrentCountry = addressDetails.CurrentCountry,
				CurrentPostalCode = addressDetails.CurrentPostalCode,
				TypeOfOwnership = addressDetails.TypeOfOwnership,
				OwnershipOtherText = OwnershipOtherText,
				PermanentAddress = addressDetails.PermanentAddress,
				PermanentCity = addressDetails.PermanentCity,
				PermanentProvince = addressDetails.PermanentProvince,
				PermanentCountry = addressDetails.PermanentCountry,
				PermanentPostalCode = addressDetails.PermanentPostalCode,
				SameAsPermanent = SameAsPermanent
			},
			EducationalBackground = new EducationalBackgroundState
			{
				HighestEducationalAttainment = educationalBackground.HighestEducationalAttainment,
				GraduationDate = GraduationDate,
				DegreeWithMajor = DegreeWithMajor,
				AcademicInstitution = AcademicInstitution,
				Diploma = FileMetadata(educationalBackground.DiplomaFileName, educationalBackground.DiplomaFile)
			},
			LicensesDetails = new LicensesDetailsState
			{
				HasProfessionalLicense = hasProfessionalLicense,
				LicenseName = licensesDetails.LicenseName,
				LicenseNumber = licensesDetails.LicenseNumber,
				LicenseExpiryDate = LicenseExpiryDate,
				LicenseDocument = FileMetadata(licensesDetails.LicenseUploadFileName, licensesDetails.LicenseUploadFile)
			},
			ProfessionalExperiences = new ProfessionalExperiencesState
			{
				AddEmployer2 = AddEmployer2,
				AddEmployer3 = AddEmployer3,
				Employer1 = CreateEmployerState(1),
				Employer2 = CreateEmployerState(2),
				Employer3 = CreateEmployerState(3)
			},
			ReferenceDetails = new ReferenceDetailsState
			{
				AddReference3 = AddAnotherReference,
				Reference1 = CreateReferenceState(1),
				Reference2 = CreateReferenceState(2),
				Reference3 = CreateReferenceState(3)
			},
			SignatureDetails = new SignatureDetailsState
			{
				Consent = consent,
				DeclineConsent = declineConsent,
				SignerName = signatureDetails.SignerName,
				SignatureDate = SignatureDate,
				HadSignature = signatureDetails.Signature is { Length: > 0 }
			}
		};
	}

	private EmployerState CreateEmployerState(int employerNumber) => employerNumber switch
	{
		1 => new EmployerState
		{
			CompanyName = professionalExperiences.Emp1CompanyName,
			CurrentlyEmployed = professionalExperiences.Emp1CurrentlyEmployed,
			PermissionToContact = professionalExperiences.Emp1PermissionToContact,
			CompanyCity = professionalExperiences.Emp1CompanyCity,
			CompanyProvince = professionalExperiences.Emp1CompanyProvince,
			CompanyCountry = professionalExperiences.Emp1CompanyCountry,
			CompanyPostalCode = professionalExperiences.Emp1CompanyPostalCode,
			DatePermittedToContact = DatePermittedToContact1,
			JobTitle = professionalExperiences.Emp1JobTitle,
			StartDate = StartOfEmployment1,
			EndDate = EndOfEmployment1,
			SupervisorName = professionalExperiences.Emp1SupervisorName,
			SupervisorContactNumber = professionalExperiences.Emp1SupervisorContactNumber,
			CertificateOfEmployment = FileMetadata(professionalExperiences.Emp1COEUploadFileName, professionalExperiences.Emp1COEUploadFile)
		},
		2 => new EmployerState
		{
			CompanyName = professionalExperiences.Emp2CompanyName,
			CurrentlyEmployed = professionalExperiences.Emp2CurrentlyEmployed,
			PermissionToContact = professionalExperiences.Emp2PermissionToContact,
			CompanyCity = professionalExperiences.Emp2CompanyCity,
			CompanyProvince = professionalExperiences.Emp2CompanyProvince,
			CompanyCountry = professionalExperiences.Emp2CompanyCountry,
			CompanyPostalCode = professionalExperiences.Emp2CompanyPostalCode,
			DatePermittedToContact = DatePermittedToContact2,
			JobTitle = professionalExperiences.Emp2JobTitle,
			StartDate = StartOfEmployment2,
			EndDate = EndOfEmployment2,
			SupervisorName = professionalExperiences.Emp2SupervisorName,
			SupervisorContactNumber = professionalExperiences.Emp2SupervisorContactNumber,
			CertificateOfEmployment = FileMetadata(professionalExperiences.Emp2COEUploadFileName, professionalExperiences.Emp2COEUploadFile)
		},
		3 => new EmployerState
		{
			CompanyName = professionalExperiences.Emp3CompanyName,
			CurrentlyEmployed = professionalExperiences.Emp3CurrentlyEmployed,
			PermissionToContact = professionalExperiences.Emp3PermissionToContact,
			CompanyCity = professionalExperiences.Emp3CompanyCity,
			CompanyProvince = professionalExperiences.Emp3CompanyProvince,
			CompanyCountry = professionalExperiences.Emp3CompanyCountry,
			CompanyPostalCode = professionalExperiences.Emp3CompanyPostalCode,
			DatePermittedToContact = DatePermittedToContact3,
			JobTitle = professionalExperiences.Emp3JobTitle,
			StartDate = StartOfEmployment3,
			EndDate = EndOfEmployment3,
			SupervisorName = professionalExperiences.Emp3SupervisorName,
			SupervisorContactNumber = professionalExperiences.Emp3SupervisorContactNumber,
			CertificateOfEmployment = FileMetadata(professionalExperiences.Emp3COEUploadFileName, professionalExperiences.Emp3COEUploadFile)
		},
		_ => throw new ArgumentOutOfRangeException(nameof(employerNumber))
	};

	private ReferenceState CreateReferenceState(int referenceNumber) => referenceNumber switch
	{
		1 => new ReferenceState
		{
			FullName = referenceDetails.Ref1FullName,
			ProfessionalRelationship = referenceDetails.Ref1ProfessionalRelationship,
			AffiliatedCompany = referenceDetails.Ref1AffiliatedCompany,
			Email = referenceDetails.Ref1Email,
			ContactNumber = referenceDetails.Ref1ContactNumber,
			ModeOfContact = referenceDetails.Ref1ModeOfContact,
			BestDate = Ref1BestDate,
			BestTime = Ref1BestTime
		},
		2 => new ReferenceState
		{
			FullName = referenceDetails.Ref2FullName,
			ProfessionalRelationship = referenceDetails.Ref2ProfessionalRelationship,
			AffiliatedCompany = referenceDetails.Ref2AffiliatedCompany,
			Email = referenceDetails.Ref2Email,
			ContactNumber = referenceDetails.Ref2ContactNumber,
			ModeOfContact = referenceDetails.Ref2ModeOfContact,
			BestDate = Ref2BestDate,
			BestTime = Ref2BestTime
		},
		3 => new ReferenceState
		{
			FullName = referenceDetails.Ref3FullName,
			ProfessionalRelationship = referenceDetails.Ref3ProfessionalRelationship,
			AffiliatedCompany = referenceDetails.Ref3AffiliatedCompany,
			Email = referenceDetails.Ref3Email,
			ContactNumber = referenceDetails.Ref3ContactNumber,
			ModeOfContact = referenceDetails.Ref3ModeOfContact,
			BestDate = Ref3BestDate,
			BestTime = Ref3BestTime
		},
		_ => throw new ArgumentOutOfRangeException(nameof(referenceNumber))
	};

	private async Task RestoreDraftAsync()
	{
		var state = await ApplicationFormStateService.LoadAsync();
		if (state is null)
			return;

		if (state.EmailInvitationId != EmailId ||
			state.Version != 1 ||
			state.PersonalDetails is null ||
			state.AddressDetails is null ||
			state.EducationalBackground is null ||
			state.LicensesDetails is null ||
			state.ProfessionalExperiences is null ||
			state.ProfessionalExperiences.Employer1 is null ||
			state.ProfessionalExperiences.Employer2 is null ||
			state.ProfessionalExperiences.Employer3 is null ||
			state.ReferenceDetails is null ||
			state.ReferenceDetails.Reference1 is null ||
			state.ReferenceDetails.Reference2 is null ||
			state.ReferenceDetails.Reference3 is null ||
			state.SignatureDetails is null)
		{
			await ApplicationFormStateService.ClearAsync();
			return;
		}

		// Uploaded files and the signature are intentionally not persisted.
		// Start restored drafts at the first step so those required inputs can be reviewed again.
		_activeStep = 0;
		RestorePersonalDetails(state.PersonalDetails);
		RestoreAddressDetails(state.AddressDetails);
		RestoreEducationalBackground(state.EducationalBackground);
		RestoreLicensesDetails(state.LicensesDetails);
		RestoreProfessionalExperiences(state.ProfessionalExperiences);
		RestoreReferenceDetails(state.ReferenceDetails);
		RestoreSignatureDetails(state.SignatureDetails);
	}

	private void RestorePersonalDetails(PersonalDetailsState state)
	{
		personalDetails.PositionAppliedFor = state.PositionAppliedFor;
		personalDetails.FirstName = state.FirstName;
		personalDetails.MiddleName = state.MiddleName;
		personalDetails.LastName = state.LastName;
		personalDetails.Suffix = state.Suffix;
		personalDetails.Sex = state.Sex;
		personalDetails.MobileNumber = state.MobileNumber;
		personalDetails.EmailAlternative = state.EmailAlternative;
		personalDetails.AdditionalGovtIDFileName = state.AdditionalGovernmentId?.FileName;
		personalDetails.NBIClearanceFileName = state.NbiClearance?.FileName;
		personalDetails.ResumeFileName = state.Resume?.FileName;
		DateOfBirth = state.DateOfBirth;
		NoMiddleName = state.NoMiddleName;
	}

	private void RestoreAddressDetails(AddressDetailsState state)
	{
		addressDetails.CurrentAddress = state.CurrentAddress;
		addressDetails.CurrentCity = state.CurrentCity;
		addressDetails.CurrentProvince = state.CurrentProvince;
		addressDetails.CurrentCountry = state.CurrentCountry;
		addressDetails.CurrentPostalCode = state.CurrentPostalCode;
		addressDetails.TypeOfOwnership = state.TypeOfOwnership;
		addressDetails.PermanentAddress = state.PermanentAddress;
		addressDetails.PermanentCity = state.PermanentCity;
		addressDetails.PermanentProvince = state.PermanentProvince;
		addressDetails.PermanentCountry = state.PermanentCountry;
		addressDetails.PermanentPostalCode = state.PermanentPostalCode;
		OwnershipOtherText = state.OwnershipOtherText;
		SameAsPermanent = state.SameAsPermanent;
	}

	private void RestoreEducationalBackground(EducationalBackgroundState state)
	{
		educationalBackground.HighestEducationalAttainment = state.HighestEducationalAttainment;
		educationalBackground.DiplomaFileName = state.Diploma?.FileName;
		GraduationDate = state.GraduationDate;
		DegreeWithMajor = state.DegreeWithMajor;
		AcademicInstitution = state.AcademicInstitution;
	}

	private void RestoreLicensesDetails(LicensesDetailsState state)
	{
		hasProfessionalLicense = state.HasProfessionalLicense;
		licensesDetails.LicenseName = state.LicenseName;
		licensesDetails.LicenseNumber = state.LicenseNumber;
		licensesDetails.LicenseUploadFileName = state.LicenseDocument?.FileName;
		LicenseExpiryDate = state.LicenseExpiryDate;
	}

	private void RestoreProfessionalExperiences(ProfessionalExperiencesState state)
	{
		AddEmployer2 = state.AddEmployer2;
		AddEmployer3 = state.AddEmployer3;
		RestoreEmployerState(1, state.Employer1);
		RestoreEmployerState(2, state.Employer2);
		RestoreEmployerState(3, state.Employer3);
	}

	private void RestoreEmployerState(int employerNumber, EmployerState state)
	{
		switch (employerNumber)
		{
			case 1:
				professionalExperiences.Emp1CompanyName = state.CompanyName;
				professionalExperiences.Emp1CurrentlyEmployed = state.CurrentlyEmployed;
				professionalExperiences.Emp1PermissionToContact = state.PermissionToContact;
				professionalExperiences.Emp1CompanyCity = state.CompanyCity;
				professionalExperiences.Emp1CompanyProvince = state.CompanyProvince;
				professionalExperiences.Emp1CompanyCountry = state.CompanyCountry;
				professionalExperiences.Emp1CompanyPostalCode = state.CompanyPostalCode;
				professionalExperiences.Emp1JobTitle = state.JobTitle;
				professionalExperiences.Emp1SupervisorName = state.SupervisorName;
				professionalExperiences.Emp1SupervisorContactNumber = state.SupervisorContactNumber;
				professionalExperiences.Emp1COEUploadFileName = state.CertificateOfEmployment?.FileName;
				DatePermittedToContact1 = state.DatePermittedToContact;
				StartOfEmployment1 = state.StartDate;
				EndOfEmployment1 = state.EndDate;
				break;
			case 2:
				professionalExperiences.Emp2CompanyName = state.CompanyName;
				professionalExperiences.Emp2CurrentlyEmployed = state.CurrentlyEmployed;
				professionalExperiences.Emp2PermissionToContact = state.PermissionToContact;
				professionalExperiences.Emp2CompanyCity = state.CompanyCity;
				professionalExperiences.Emp2CompanyProvince = state.CompanyProvince;
				professionalExperiences.Emp2CompanyCountry = state.CompanyCountry;
				professionalExperiences.Emp2CompanyPostalCode = state.CompanyPostalCode;
				professionalExperiences.Emp2JobTitle = state.JobTitle;
				professionalExperiences.Emp2SupervisorName = state.SupervisorName;
				professionalExperiences.Emp2SupervisorContactNumber = state.SupervisorContactNumber;
				professionalExperiences.Emp2COEUploadFileName = state.CertificateOfEmployment?.FileName;
				DatePermittedToContact2 = state.DatePermittedToContact;
				StartOfEmployment2 = state.StartDate;
				EndOfEmployment2 = state.EndDate;
				break;
			case 3:
				professionalExperiences.Emp3CompanyName = state.CompanyName;
				professionalExperiences.Emp3CurrentlyEmployed = state.CurrentlyEmployed;
				professionalExperiences.Emp3PermissionToContact = state.PermissionToContact;
				professionalExperiences.Emp3CompanyCity = state.CompanyCity;
				professionalExperiences.Emp3CompanyProvince = state.CompanyProvince;
				professionalExperiences.Emp3CompanyCountry = state.CompanyCountry;
				professionalExperiences.Emp3CompanyPostalCode = state.CompanyPostalCode;
				professionalExperiences.Emp3JobTitle = state.JobTitle;
				professionalExperiences.Emp3SupervisorName = state.SupervisorName;
				professionalExperiences.Emp3SupervisorContactNumber = state.SupervisorContactNumber;
				professionalExperiences.Emp3COEUploadFileName = state.CertificateOfEmployment?.FileName;
				DatePermittedToContact3 = state.DatePermittedToContact;
				StartOfEmployment3 = state.StartDate;
				EndOfEmployment3 = state.EndDate;
				break;
		}
	}

	private void RestoreReferenceDetails(ReferenceDetailsState state)
	{
		AddAnotherReference = state.AddReference3;
		RestoreReferenceState(1, state.Reference1);
		RestoreReferenceState(2, state.Reference2);
		RestoreReferenceState(3, state.Reference3);
	}

	private void RestoreReferenceState(int referenceNumber, ReferenceState state)
	{
		switch (referenceNumber)
		{
			case 1:
				referenceDetails.Ref1FullName = state.FullName;
				referenceDetails.Ref1ProfessionalRelationship = state.ProfessionalRelationship;
				referenceDetails.Ref1AffiliatedCompany = state.AffiliatedCompany;
				referenceDetails.Ref1Email = state.Email;
				referenceDetails.Ref1ContactNumber = state.ContactNumber;
				referenceDetails.Ref1ModeOfContact = state.ModeOfContact;
				Ref1BestDate = state.BestDate;
				Ref1BestTime = state.BestTime;
				break;
			case 2:
				referenceDetails.Ref2FullName = state.FullName;
				referenceDetails.Ref2ProfessionalRelationship = state.ProfessionalRelationship;
				referenceDetails.Ref2AffiliatedCompany = state.AffiliatedCompany;
				referenceDetails.Ref2Email = state.Email;
				referenceDetails.Ref2ContactNumber = state.ContactNumber;
				referenceDetails.Ref2ModeOfContact = state.ModeOfContact;
				Ref2BestDate = state.BestDate;
				Ref2BestTime = state.BestTime;
				break;
			case 3:
				referenceDetails.Ref3FullName = state.FullName;
				referenceDetails.Ref3ProfessionalRelationship = state.ProfessionalRelationship;
				referenceDetails.Ref3AffiliatedCompany = state.AffiliatedCompany;
				referenceDetails.Ref3Email = state.Email;
				referenceDetails.Ref3ContactNumber = state.ContactNumber;
				referenceDetails.Ref3ModeOfContact = state.ModeOfContact;
				Ref3BestDate = state.BestDate;
				Ref3BestTime = state.BestTime;
				break;
		}
	}

	private void RestoreSignatureDetails(SignatureDetailsState state)
	{
		consent = state.Consent;
		declineConsent = state.DeclineConsent;
		signatureDetails.SignerName = state.SignerName;
		SignatureDate = state.SignatureDate;
	}

	private async Task ClearDraftAsync()
	{
		_draftCleared = true;
		_draftSaveCancellation?.Cancel();
		await ApplicationFormStateService.ClearAsync();
	}

	public async Task ResetApplicationFormAsync()
	{
		personalDetails = new();
		addressDetails = new();
		educationalBackground = new();
		licensesDetails = new();
		professionalExperiences = new();
		referenceDetails = new();
		signatureDetails = new();
		DateOfBirth = null;
		GraduationDate = null;
		DegreeWithMajor = null;
		AcademicInstitution = null;
		LicenseExpiryDate = null;
		DatePermittedToContact1 = StartOfEmployment1 = EndOfEmployment1 = null;
		DatePermittedToContact2 = StartOfEmployment2 = EndOfEmployment2 = null;
		DatePermittedToContact3 = StartOfEmployment3 = EndOfEmployment3 = null;
		Ref1BestDate = Ref2BestDate = Ref3BestDate = null;
		Ref1BestTime = Ref2BestTime = Ref3BestTime = null;
		NoMiddleName = SameAsPermanent = hasProfessionalLicense = false;
		AddEmployer2 = AddEmployer3 = AddAnotherReference = false;
		consent = declineConsent = false;
		OwnershipOtherText = null;
		SignatureDate = DateTime.UtcNow;
		_activeStep = 0;
		await ClearDraftAsync();
		await InvokeAsync(StateHasChanged);
	}

	private static FileState FileMetadata(string? fileName, byte[]? content) => new()
	{
		FileName = fileName,
		HasFile = content is { Length: > 0 }
	};

	public async ValueTask DisposeAsync()
	{
		_draftSaveCancellation?.Cancel();
		_draftSaveCancellation?.Dispose();

		if (_draftPersistenceEnabled && !_draftCleared && !IsSuccess)
			await ApplicationFormStateService.SaveAsync(CreateDraftState());
	}
}
