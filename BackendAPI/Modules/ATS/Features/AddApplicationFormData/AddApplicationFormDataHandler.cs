namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataCommand(PersonalDetailsDTO PersonalDetails,
											AddressDetailsDTO AddressDetails, 
											EducationalBackgroundDTO EducationalBackground, 
											LicensesDetailsDTO LicensesDetails, 
											ProfessionalExperiencesDTO ProfessionalExperiences, 
											ReferenceDetailsDTO ReferenceDetails,
											SignatureDetailsDTO SignatureDetails) : ICommand<AddApplicationFormDataResult>;
public record AddApplicationFormDataResult(bool IsAdded);
public class AddApplicationFormDataCommandValidator : AbstractValidator<AddApplicationFormDataCommand>
{
	public AddApplicationFormDataCommandValidator()
	{
		//personal
		RuleFor(x => x.PersonalDetails)
			.NotNull()
			.WithMessage("Personal details are required.");

		When(x => x.PersonalDetails != null, () =>
		{
			RuleFor(x => x.PersonalDetails.PositionAppliedFor)
				.NotEmpty().WithMessage("Position applied for is required.")
				.MaximumLength(50).WithMessage("Position applied must not exceed 50 characters.");

			RuleFor(x => x.PersonalDetails.FirstName)
				.NotEmpty().WithMessage("First name is required.")
				.MaximumLength(50).WithMessage("First name must not exceed 50 characters.");

			RuleFor(x => x.PersonalDetails.LastName)
				.NotEmpty().WithMessage("Last name is required.")
				.MaximumLength(50).WithMessage("Last name must not exceed 50 characters.");

			RuleFor(x => x.PersonalDetails.Sex)
				.NotEmpty()
				.WithMessage("Sex is required.");

			RuleFor(x => x.PersonalDetails.Suffix)
				.NotEmpty()
				.WithMessage("Suffix is required.");

			RuleFor(x => x.PersonalDetails.DOB)
				.NotNull().WithMessage("Date of birth is required.");

			RuleFor(x => x.PersonalDetails.MobileNumber)
				.NotEmpty().WithMessage("Mobile number is required.")
				.MaximumLength(11).WithMessage("Mobile number must not exceed 11 characters.");

			RuleFor(x => x.PersonalDetails.EmailAlternative)
				.NotEmpty()
				.EmailAddress()
				.WithMessage("A valid email address is required.");

			RuleFor(x => x.PersonalDetails.AdditionalGovtIDFile)
				.NotNull().WithMessage("Government ID is required.");

			RuleFor(x => x.PersonalDetails.NBIClearanceFile)
				.NotNull().WithMessage("NBI Clearance is required.");

			RuleFor(x => x.PersonalDetails.ResumeFile)
				.NotNull().WithMessage("Resume is required.");
		});

		//address
		RuleFor(x => x.AddressDetails)
			.NotNull().WithMessage("Address details are required.");

		When(x => x.AddressDetails != null, () =>
		{
			// current
			RuleFor(x => x.AddressDetails.CurrentAddress)
				.NotEmpty()
				.WithMessage("Current Address (No., Street, Village, Brgy.) is required.");

			RuleFor(x => x.AddressDetails.CurrentCity)
				.NotEmpty()
				.WithMessage("City is required.");

			RuleFor(x => x.AddressDetails.CurrentProvince)
				.NotEmpty()
				.WithMessage("State / Province / Region is required.");

			RuleFor(x => x.AddressDetails.CurrentCountry)
				.NotEmpty()
				.WithMessage("Country is required.");

			RuleFor(x => x.AddressDetails.CurrentPostalCode)
				.NotEmpty()
				.WithMessage("Zip / Postal Code is required.");

			// perma
			RuleFor(x => x.AddressDetails.PermanentAddress)
				.NotEmpty()
				.WithMessage("Permanent Address (No., Street, Village, Brgy.) is required.");

			RuleFor(x => x.AddressDetails.PermanentCity)
				.NotEmpty()
				.WithMessage("City is required.");

			RuleFor(x => x.AddressDetails.PermanentProvince)
				.NotEmpty()
				.WithMessage("State / Province / Region is required.");

			RuleFor(x => x.AddressDetails.PermanentCountry)
				.NotEmpty()
				.WithMessage("Country is required.");

			RuleFor(x => x.AddressDetails.PermanentPostalCode)
				.NotEmpty()
				.WithMessage("Zip / Postal Code is required.");

			// ownership
			RuleFor(x => x.AddressDetails.TypeOfOwnership)
				.NotEmpty()
				.WithMessage("Type of Ownership is required.");
		});

		//for educ
		RuleFor(x => x.EducationalBackground)
			.NotNull()
			.WithMessage("Educational background is required.");

		When(x => x.EducationalBackground != null, () =>
		{
			RuleFor(x => x.EducationalBackground.HighestEducationalAttainment)
				.NotEmpty()
				.WithMessage("Highest educational attainment is required.");

			// junior
			When(x => x.EducationalBackground.HighestEducationalAttainment == "Junior High School Graduate", () =>
			{
				RuleFor(x => x.EducationalBackground.HighSchoolName)
					.NotEmpty()
					.WithMessage("School name is required.");

				RuleFor(x => x.EducationalBackground.HighSchoolGraduationDate)
					.NotNull()
					.WithMessage("Graduation date is required.");

				RuleFor(x => x.EducationalBackground.HighSchoolDiplomaFile)
					.NotNull()
					.WithMessage("Diploma is required.");
			});

			// senior
			When(x => x.EducationalBackground.HighestEducationalAttainment == "Senior High School Graduate", () =>
			{
				RuleFor(x => x.EducationalBackground.SeniorHighSchoolName)
					.NotEmpty()
					.WithMessage("School name is required.");

				RuleFor(x => x.EducationalBackground.SeniorHighSchoolGraduationDate)
					.NotNull()
					.WithMessage("Graduation date is required.");

				RuleFor(x => x.EducationalBackground.SeniorHighSchoolDiplomaFile)
					.NotNull()
					.WithMessage("Diploma is required.");
			});

			// college
			When(x => x.EducationalBackground.HighestEducationalAttainment == "College Graduate", () =>
			{
				RuleFor(x => x.EducationalBackground.BachelorsSchoolName)
					.NotEmpty()
					.WithMessage("School name is required.");

				RuleFor(x => x.EducationalBackground.BachelorsDegree)
					.NotEmpty()
					.WithMessage("Degree is required.");

				RuleFor(x => x.EducationalBackground.BachelorsGraduationDate)
					.NotNull()
					.WithMessage("Graduation date is required.");

				RuleFor(x => x.EducationalBackground.BachelorsDiplomaFile)
					.NotNull()
					.WithMessage("Diploma is required.");
			});

			// master
			When(x => x.EducationalBackground.HighestEducationalAttainment == "Master's Graduate", () =>
			{
				RuleFor(x => x.EducationalBackground.MastersSchoolName)
					.NotEmpty()
					.WithMessage("School name is required.");

				RuleFor(x => x.EducationalBackground.MastersDegree)
					.NotEmpty()
					.WithMessage("Degree is required.");

				RuleFor(x => x.EducationalBackground.MastersGraduationDate)
					.NotNull()
					.WithMessage("Graduation date is required.");

				RuleFor(x => x.EducationalBackground.MastersDiplomaFile)
					.NotNull()
					.WithMessage("Diploma is required.");
			});

			// doc
			When(x => x.EducationalBackground.HighestEducationalAttainment == "Doctorate Graduate", () =>
			{
				RuleFor(x => x.EducationalBackground.PhDSchoolName)
					.NotEmpty()
					.WithMessage("School name is required.");

				RuleFor(x => x.EducationalBackground.DoctorateDegree)
					.NotEmpty()
					.WithMessage("Degree is required.");

				RuleFor(x => x.EducationalBackground.DoctorateGraduationDate)
					.NotNull()
					.WithMessage("Graduation date is required.");

				RuleFor(x => x.EducationalBackground.DoctorateDiplomaFile)
					.NotNull()
					.WithMessage("Diploma is required.");
			});

			// none/elem
			When(x =>
				x.EducationalBackground.HighestEducationalAttainment == "None" ||
				x.EducationalBackground.HighestEducationalAttainment == "Elementary Graduate",
				() =>
				{
					
				});
		});

		//for license
		RuleFor(x => x.LicensesDetails)
			.NotNull().WithMessage("Licenses details are required.");

		When(x => x.LicensesDetails != null, () =>
		{
			When(x =>
				!string.IsNullOrWhiteSpace(x.LicensesDetails.LicenseName) ||
				!string.IsNullOrWhiteSpace(x.LicensesDetails.LicenseNumber) ||
				x.LicensesDetails.LicenseExpiryDate.HasValue ||
				x.LicensesDetails.LicenseUploadFile != null,
			() =>
			{
				RuleFor(x => x.LicensesDetails.LicenseName)
					.NotEmpty()
					.WithMessage("Professional License is required.");

				RuleFor(x => x.LicensesDetails.LicenseNumber)
					.NotEmpty()
					.WithMessage("License No. is required.");

				RuleFor(x => x.LicensesDetails.LicenseExpiryDate)
					.NotNull()
					.WithMessage("License Expiry Date is required.");

				RuleFor(x => x.LicensesDetails.LicenseUploadFile)
					.NotNull()
					.WithMessage("Professional License is required.");
			});
		});

		//for professional experiences
		RuleFor(x => x.ProfessionalExperiences)
			.NotNull().WithMessage("Professional experiences are required.");

		When(x => x.ProfessionalExperiences != null, () =>
		{
			//employer 1
			RuleFor(x => x.ProfessionalExperiences.Emp1CompanyName)
				.NotEmpty()
				.WithMessage("Company Name #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1CompanyCity)
				.NotEmpty()
				.WithMessage("Company City / Address #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1CompanyProvince)
				.NotEmpty()
				.WithMessage("State / Province / Region #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1CompanyCountry)
				.NotEmpty()
				.WithMessage("Country #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1CompanyPostalCode)
				.NotEmpty()
				.WithMessage("Zip Code #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1DatePermittedToContact)
				.NotNull()
				.WithMessage("Date permitted to contact #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1JobTitle)
				.NotEmpty()
				.WithMessage("Job Title #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1StartDate)
				.NotNull()
				.WithMessage("Start Date of Employment #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1EndDate)
				.NotNull()
				.WithMessage("End Date of Employment #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1SupervisorName)
				.NotEmpty()
				.WithMessage("Supervisor Name #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1SupervisorContactNumber)
				.NotEmpty()
				.WithMessage("Supervisor Contact Number #1 is required.");

			RuleFor(x => x.ProfessionalExperiences.Emp1COEUploadFile)
				.NotNull()
				.WithMessage("COE/ID #1 is required.");
		
			//employer 2
			When(x =>
				!string.IsNullOrWhiteSpace(x.ProfessionalExperiences.Emp2CompanyName) ||
				!string.IsNullOrWhiteSpace(x.ProfessionalExperiences.Emp2CompanyCity) ||
				!string.IsNullOrWhiteSpace(x.ProfessionalExperiences.Emp2JobTitle) ||
				x.ProfessionalExperiences.Emp2StartDate.HasValue ||
				x.ProfessionalExperiences.Emp2COEUploadFile != null,
				() =>
				{
				RuleFor(x => x.ProfessionalExperiences.Emp2CompanyName)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2CompanyCity)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2CompanyProvince)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2CompanyCountry)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2CompanyPostalCode)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2DatePermittedToContact)
					.NotNull();

				RuleFor(x => x.ProfessionalExperiences.Emp2JobTitle)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2StartDate)
					.NotNull();

				RuleFor(x => x.ProfessionalExperiences.Emp2EndDate)
					.NotNull();

				RuleFor(x => x.ProfessionalExperiences.Emp2SupervisorName)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2SupervisorContactNumber)
					.NotEmpty();

				RuleFor(x => x.ProfessionalExperiences.Emp2COEUploadFile)
					.NotNull();
				});

		//employer 3
		When(x =>
			!string.IsNullOrWhiteSpace(x.ProfessionalExperiences.Emp3CompanyName) ||
			!string.IsNullOrWhiteSpace(x.ProfessionalExperiences.Emp3CompanyCity) ||
			!string.IsNullOrWhiteSpace(x.ProfessionalExperiences.Emp3JobTitle) ||
			x.ProfessionalExperiences.Emp3StartDate.HasValue ||
			x.ProfessionalExperiences.Emp3COEUploadFile != null,
		() =>
		{
			RuleFor(x => x.ProfessionalExperiences.Emp3CompanyName)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3CompanyCity)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3CompanyProvince)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3CompanyCountry)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3CompanyPostalCode)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3DatePermittedToContact)
				.NotNull();

			RuleFor(x => x.ProfessionalExperiences.Emp3JobTitle)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3StartDate)
				.NotNull();

			RuleFor(x => x.ProfessionalExperiences.Emp3EndDate)
				.NotNull();

			RuleFor(x => x.ProfessionalExperiences.Emp3SupervisorName)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3SupervisorContactNumber)
				.NotEmpty();

			RuleFor(x => x.ProfessionalExperiences.Emp3COEUploadFile)
				.NotNull();

			});
		});

		//for reference
		RuleFor(x => x.ReferenceDetails)
			.NotNull().WithMessage("Reference details are required.");

		When(x => x.ReferenceDetails != null, () =>
		{
			// ref 1
			RuleFor(x => x.ReferenceDetails.Ref1FullName)
				.NotEmpty()
				.WithMessage("Full Name of Reference #1 is required.");

			RuleFor(x => x.ReferenceDetails.Ref1ProfessionalRelationship)
				.NotEmpty()
				.WithMessage("Professional Relationship #1 is required.");

			RuleFor(x => x.ReferenceDetails.Ref1AffiliatedCompany)
				.NotEmpty()
				.WithMessage("Affiliated Company #1 is required.");

			RuleFor(x => x.ReferenceDetails.Ref1Email)
				.NotEmpty()
				.WithMessage("Email Contact Information #1 is required.")
				.EmailAddress()
				.WithMessage("Email Contact Information #1 is invalid.");

			RuleFor(x => x.ReferenceDetails.Ref1ContactNumber)
				.NotEmpty()
				.Matches(@"^\d{11}$")
				.WithMessage("Mobile Contact Information #1 must be 11 digits.");

			RuleFor(x => x.ReferenceDetails.Ref1ModeOfContact)
				.NotEmpty()
				.WithMessage("Mode of Contact #1 is required.");

			RuleFor(x => x.ReferenceDetails.Ref1BestTimeToContact)
				.NotNull()
				.WithMessage("Best Time to Contact #1 is required.");

			// ref 2
			RuleFor(x => x.ReferenceDetails.Ref2FullName)
				.NotEmpty()
				.WithMessage("Full Name of Reference #2 is required.");

			RuleFor(x => x.ReferenceDetails.Ref2ProfessionalRelationship)
				.NotEmpty()
				.WithMessage("Professional Relationship #2 is required.");

			RuleFor(x => x.ReferenceDetails.Ref2AffiliatedCompany)
				.NotEmpty()
				.WithMessage("Affiliated Company #2 is required.");

			RuleFor(x => x.ReferenceDetails.Ref2Email)
				.NotEmpty()
				.WithMessage("Email Contact Information #2 is required.")
				.EmailAddress()
				.WithMessage("Email Contact Information #2 is invalid.");

			RuleFor(x => x.ReferenceDetails.Ref2ContactNumber)
				.NotEmpty()
				.Matches(@"^\d{11}$")
				.WithMessage("Mobile Contact Information #2 must be 11 digits.");

			RuleFor(x => x.ReferenceDetails.Ref2ModeOfContact)
				.NotEmpty()
				.WithMessage("Mode of Contact #2 is required.");

			RuleFor(x => x.ReferenceDetails.Ref2BestTimeToContact)
				.NotNull()
				.WithMessage("Best Time to Contact #2 is required.");

			// ref 3
			When(x =>
				!string.IsNullOrWhiteSpace(x.ReferenceDetails.Ref3FullName) ||
				!string.IsNullOrWhiteSpace(x.ReferenceDetails.Ref3AffiliatedCompany) ||
				!string.IsNullOrWhiteSpace(x.ReferenceDetails.Ref3Email) ||
				!string.IsNullOrWhiteSpace(x.ReferenceDetails.Ref3ContactNumber) ||
				x.ReferenceDetails.Ref3BestTimeToContact.HasValue,
			() =>
			{
				RuleFor(x => x.ReferenceDetails.Ref3FullName)
					.NotEmpty()
					.WithMessage("Full Name of Reference #3 is required.");

				RuleFor(x => x.ReferenceDetails.Ref3ProfessionalRelationship)
					.NotEmpty()
					.WithMessage("Professional Relationship #3 is required.");

				RuleFor(x => x.ReferenceDetails.Ref3AffiliatedCompany)
					.NotEmpty()
					.WithMessage("Affiliated Company #3 is required.");

				RuleFor(x => x.ReferenceDetails.Ref3Email)
					.NotEmpty()
					.WithMessage("Email Contact Information #3 is required.")
					.EmailAddress()
					.WithMessage("Email Contact Information #3 is invalid.");

				RuleFor(x => x.ReferenceDetails.Ref3ContactNumber)
					.NotEmpty()
					.Matches(@"^\d{11}$")
					.WithMessage("Mobile Contact Information #3 must be 11 digits.");

				RuleFor(x => x.ReferenceDetails.Ref3ModeOfContact)
					.NotEmpty()
					.WithMessage("Mode of Contact #3 is required.");

				RuleFor(x => x.ReferenceDetails.Ref3BestTimeToContact)
					.NotNull()
					.WithMessage("Best Time to Contact #3 is required.");
			});
		});

		//for signature
		RuleFor(x => x.SignatureDetails)
			.NotNull().WithMessage("Signature details are required.");

		When(x => x.SignatureDetails != null, () =>
		{
			RuleFor(x => x.SignatureDetails.Signature)
				.NotNull()
				.WithMessage("Signature is required.");

			RuleFor(x => x.SignatureDetails.SignerName)
				.NotEmpty()
				.WithMessage("Name is required.");

			RuleFor(x => x.SignatureDetails.SignatureDate)
				.Must(d => d != default)
				.WithMessage("Date is required.");
		});
	}
}

public class AddApplicationFormDataHandler : ICommandHandler<AddApplicationFormDataCommand, AddApplicationFormDataResult>
{
	private readonly IApplicationFormService _applicationFormService;
	public AddApplicationFormDataHandler(IApplicationFormService applicationFormService)
	{
		_applicationFormService = applicationFormService;
	}
	public async Task<AddApplicationFormDataResult> Handle(AddApplicationFormDataCommand request, CancellationToken cancellationToken)
	{
		var result = await _applicationFormService.AddApplicationFormDataAsync(request.PersonalDetails, 
																			   request.AddressDetails, 
																   request.EducationalBackground, 
																   request.LicensesDetails, 
																   request.ProfessionalExperiences,
																   request.ReferenceDetails,
																   request.SignatureDetails,
																   cancellationToken);
		return new AddApplicationFormDataResult(result);
	}
}
