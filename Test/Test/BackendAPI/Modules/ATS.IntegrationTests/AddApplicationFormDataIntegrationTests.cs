using ATS.Data.Entities;
using ATS.DTO;
using ATS.Features.AddApplicationFormData;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class AddApplicationFormDataIntegrationTests : BaseIntegrationTest
{
	private readonly string _atsTestFolder;
	Guid EmailId = Guid.NewGuid();

	public AddApplicationFormDataIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_atsTestFolder = _configuration
						.GetSection("AlibabaOss")
						.GetValue<string>("ATSTestFolder") ?? string.Empty;
	}

	private IFormFile CreateFakeFormFile(byte[] content, string fileName)
	{
		var stream = new MemoryStream(content);
		return new FormFile(stream, 0, content.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = "text/plain"
		};
	}

	[Fact]
	public async Task AddApplicationFormData_WithSamplePayload_ShouldReturnTrue()
	{
		await SeedEmailInvitationRequestData();

		byte[] sampleFileContent = Convert.FromBase64String("SGVsbG8gV29ybGQ=");
		DateOnly sampleDate = DateOnly.FromDateTime(DateTime.UtcNow);
		var govermentIdFileName = $"{Guid.NewGuid}-govId.txt";
		var nbiFileName = $"{Guid.NewGuid}-nbiId.txt";
		var resumeFileName = $"{Guid.NewGuid}-govId.txt";
		var highSchoolDiplomaFileName = $"{Guid.NewGuid}-highSchoolDiploma.txt";
		var seniorHighSchoolDiplomaFileName = $"{Guid.NewGuid}-seniorHighSchoolDiplomaFileName.txt";
		var bachelorSchoolDiplomaFileName = $"{Guid.NewGuid}-bachelorSchoolDiplomaFileName.txt";
		var masterSchoolDiplomaFileName = $"{Guid.NewGuid}-masterSchoolDiplomaFileName.txt";
		var doctorateSchoolDiplomaFileName = $"{Guid.NewGuid}-doctorateSchoolDiplomaFileName.txt";
		var licenseFileName = $"{Guid.NewGuid}-licenseFileName.txt";
		var emp1COEFileName = $"{Guid.NewGuid}-emp1COEFileName.txt";
		var emp2COEFileName = $"{Guid.NewGuid}-emp2COEFileName.txt";
		var emp3COEFileName = $"{Guid.NewGuid}-emp3COEFileName.txt";

		var personal = new PersonalDetailsDTO
		{
			EmailInvitationID = EmailId,
			PersonalID = Guid.NewGuid(),
			PositionAppliedFor = "Senior Software Engineer",
			FirstName = "Juan",
			MiddleName = "Santos",
			LastName = "Dela Cruz",
			Suffix = "Jr.",
			Sex = "Male",
			DOB = sampleDate,
			MobileNumber = "+639171234567",
			EmailAlternative = "juan.delacruz@gmail.com",
			AdditionalGovtIDFile = CreateFakeFormFile(sampleFileContent, govermentIdFileName),
			AdditionalGovtIDFileName = govermentIdFileName,
			NBIClearanceFile = CreateFakeFormFile(sampleFileContent, nbiFileName),
			NBIClearanceFileName = nbiFileName,
			ResumeFile = CreateFakeFormFile(sampleFileContent, resumeFileName),
			ResumeFileName = resumeFileName,
			CreatedDate = DateTime.UtcNow,
		};

		var address = new AddressDetailsDTO
		{
			Address = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			CurrentCity = "Manila",
			CurrentProvince = "Metro Manila",
			CurrentCountry = "Philippines",
			CurrentAddress = "123 Rizal Avenue, Quiapo",
			CurrentPostalCode = "1001",
			TypeOfOwnership = "Owned",
			PermanentAddress = "456 Mabini Street, Sampaloc",
			PermanentCity = "Manila",
			PermanentProvince = "Metro Manila",
			PermanentCountry = "Philippines",
			PermanentPostalCode = "1008",
			CreatedDate = DateTime.UtcNow,
		};

		var education = new EducationalBackgroundDTO
		{
			EducationalBackgroundID = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			HighestEducationalAttainment = "Bachelor's Degree",
			HighSchoolName = "Manila Science High School",
			HighSchoolGraduationDate = sampleDate,
			HighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, highSchoolDiplomaFileName),
			HighSchoolDiplomaFileName = highSchoolDiplomaFileName,
			SeniorHighSchoolName = "UST Senior High School",
			SeniorHighSchoolGraduationDate = sampleDate,
			SeniorHighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, seniorHighSchoolDiplomaFileName),
			SeniorHighSchoolDiplomaFileName = highSchoolDiplomaFileName,
			BachelorsSchoolName = "University of Santo Tomas",
			BachelorsGraduationDate = sampleDate,
			BachelorsDiplomaFile = CreateFakeFormFile(sampleFileContent, bachelorSchoolDiplomaFileName),
			BachelorsDiplomaFileName = bachelorSchoolDiplomaFileName,
			BachelorsDegree = "Computer Science",
			MastersSchoolName = "Ateneo de Manila University",
			MastersGraduationDate = sampleDate,
			MastersDiplomaFile = CreateFakeFormFile(sampleFileContent, masterSchoolDiplomaFileName),
			MastersDiplomaFileName = masterSchoolDiplomaFileName,
			MastersDegree = "Information Technology",
			PhDSchoolName = string.Empty,
			DoctorateGraduationDate = sampleDate,
			DoctorateDiplomaFile = CreateFakeFormFile(sampleFileContent, doctorateSchoolDiplomaFileName),
			DoctorateDiplomaFileName = doctorateSchoolDiplomaFileName,
			DoctorateDegree = string.Empty,
			CreatedDate = DateTime.UtcNow,
		};

		var licenses = new LicensesDetailsDTO
		{
			LicensesDetailsID = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			LicenseName = "AWS Certified Developer",
			LicenseNumber = "AWS-DEV-2026-001",
			LicenseExpiryDate = sampleDate,
			LicenseUploadFile = CreateFakeFormFile(sampleFileContent, licenseFileName),
			LicenseUploadFileName = licenseFileName,
			CreatedDate = DateTime.UtcNow
		};

		var experiences = new ProfessionalExperiencesDTO
		{
			ProfessionalExperiencesID = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			Emp1CompanyName = "Accenture Philippines",
			Emp1CurrentlyEmployed = false,
			Emp1PermissionToContact = true,
			Emp1CompanyCity = "Taguig",
			Emp1CompanyProvince = "Metro Manila",
			Emp1CompanyCountry = "Philippines",
			Emp1CompanyPostalCode = "1630",
			Emp1StartDate = sampleDate,
			Emp1EndDate = sampleDate,
			Emp1JobTitle = "Software Engineer",
			Emp1SupervisorName = "Maria Santos",
			Emp1SupervisorContactNumber = "+639171234567",
			Emp1COEUploadFile = CreateFakeFormFile(sampleFileContent, emp1COEFileName),
			Emp1COEUploadFileName = emp1COEFileName,
			Emp2CompanyName = "Globe Telecom",
			Emp2CurrentlyEmployed = false,
			Emp2PermissionToContact = true,
			Emp2CompanyCity = "Makati",
			Emp2CompanyProvince = "Metro Manila",
			Emp2CompanyCountry = "Philippines",
			Emp2CompanyPostalCode = "1200",
			Emp2StartDate = sampleDate,
			Emp2EndDate = sampleDate,
			Emp2JobTitle = "Senior Backend Developer",
			Emp2SupervisorName = "Carlos Reyes",
			Emp2SupervisorContactNumber = "+639189876543",
			Emp2COEUploadFile = CreateFakeFormFile(sampleFileContent, emp2COEFileName),
			Emp2COEUploadFileName = emp2COEFileName,
			Emp3CompanyName = "Tech Innovators Inc.",
			Emp3CurrentlyEmployed = true,
			Emp3PermissionToContact = true,
			Emp3CompanyCity = "Pasig",
			Emp3CompanyProvince = "Metro Manila",
			Emp3CompanyCountry = "Philippines",
			Emp3CompanyPostalCode = "1605",
			Emp3StartDate = sampleDate,
			Emp3EndDate = sampleDate,
			Emp3JobTitle = "Lead .NET Developer",
			Emp3SupervisorName = "Ana Lopez",
			Emp3SupervisorContactNumber = "+639155551234",
			Emp3COEUploadFile = CreateFakeFormFile(sampleFileContent, emp3COEFileName),
			Emp3COEUploadFileName = emp3COEFileName,
			CreatedDate = DateTime.UtcNow,
		};

		var reference = new ReferenceDetailsDTO
		{
			ReferenceDetailsID = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			Ref1FullName = "Michael Tan",
			Ref1ProfessionalRelationship = "Former Team Lead",
			Ref1AffiliatedCompany = "Accenture Philippines",
			Ref1Email = "michael.tan@accenture.com",
			Ref1ContactNumber = "+639171111111",
			Ref1ModeOfContact = "Email",
			Ref1BestTimeToContact = DateTime.UtcNow,
			Ref2FullName = "Sarah Lim",
			Ref2ProfessionalRelationship = "Project Manager",
			Ref2AffiliatedCompany = "Globe Telecom",
			Ref2Email = "sarah.lim@globe.com.ph",
			Ref2ContactNumber = "+639172222222",
			Ref2ModeOfContact = "Phone",
			Ref2BestTimeToContact = DateTime.UtcNow,
			Ref3FullName = "John Bautista",
			Ref3ProfessionalRelationship = "Engineering Director",
			Ref3AffiliatedCompany = "Tech Innovators Inc.",
			Ref3Email = "john.bautista@techinnovators.com",
			Ref3ContactNumber = "+639173333333",
			Ref3ModeOfContact = "Email",
			Ref3BestTimeToContact = DateTime.UtcNow,
			CreatedDate = DateTime.UtcNow
		};

		var command = new AddApplicationFormDataCommand(personal, address, education, licenses, experiences, reference);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.IsAdded.Should().BeTrue();

		if (result.IsAdded == true)
		{
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{govermentIdFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{nbiFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{resumeFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{highSchoolDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{seniorHighSchoolDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{bachelorSchoolDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{masterSchoolDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{doctorateSchoolDiplomaFileName}");			
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{licenseFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{emp1COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{emp2COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{emp3COEFileName}");
		}
	}

	[Fact]
	public async Task AddApplicationFormData_MissingPersonal_ShouldThrow()
	{
		await SeedEmailInvitationRequestData();

		byte[] sampleFileContent = Convert.FromBase64String("SGVsbG8gV29ybGQ=");
		DateOnly sampleDate = DateOnly.FromDateTime(DateTime.UtcNow);

		var address = new AddressDetailsDTO
		{
			Address = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			CurrentCity = "Manila",
			CurrentProvince = "Metro Manila",
			CurrentCountry = "Philippines",
			CurrentAddress = "123 Rizal Avenue, Quiapo",
			CurrentPostalCode = "1001",
			TypeOfOwnership = "Owned",
			PermanentAddress = "456 Mabini Street, Sampaloc",
			PermanentCity = "Manila",
			PermanentProvince = "Metro Manila",
			PermanentCountry = "Philippines",
			PermanentPostalCode = "1008",
			CreatedDate = DateTime.UtcNow,
		};

		var education = new EducationalBackgroundDTO
		{
			EducationalBackgroundID = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			HighestEducationalAttainment = "Bachelor's Degree",
			HighSchoolName = "Manila Science High School",
			HighSchoolGraduationDate = sampleDate,
			HighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, "highschool_diploma.txt"),
			HighSchoolDiplomaFileName = "highschool_diploma.txt",
			SeniorHighSchoolName = "UST Senior High School",
			SeniorHighSchoolGraduationDate = sampleDate,
			SeniorHighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, "shs_diploma.txt"),
			SeniorHighSchoolDiplomaFileName = "shs_diploma.txt",
			BachelorsSchoolName = "University of Santo Tomas",
			BachelorsGraduationDate = sampleDate,
			BachelorsDiplomaFile = CreateFakeFormFile(sampleFileContent, "bachelors_diploma.txt"),
			BachelorsDiplomaFileName = "bachelors_diploma.txt",
			BachelorsDegree = "Computer Science",
			MastersSchoolName = "Ateneo de Manila University",
			MastersGraduationDate = sampleDate,
			MastersDiplomaFile = CreateFakeFormFile(sampleFileContent, "masters_diploma.txt"),
			MastersDiplomaFileName = "masters_diploma.txt",
			MastersDegree = "Information Technology",
			PhDSchoolName = string.Empty,
			DoctorateGraduationDate = sampleDate,
			DoctorateDiplomaFile = CreateFakeFormFile(Array.Empty<byte>(), "doctorate_diploma.txt"),
			DoctorateDiplomaFileName = "doctorate_diploma.txt",
			DoctorateDegree = string.Empty,
			CreatedDate = DateTime.UtcNow,
		};

		var licenses = new LicensesDetailsDTO
		{
			LicensesDetailsID = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			LicenseName = "AWS Certified Developer",
			LicenseNumber = "AWS-DEV-2026-001",
			LicenseExpiryDate = sampleDate,
			LicenseUploadFile = CreateFakeFormFile(sampleFileContent, "aws_certificate.txt"),
			LicenseUploadFileName = "aws_certificate.txt",
			CreatedDate = DateTime.UtcNow
		};

		var experiences = new ProfessionalExperiencesDTO
		{
			ProfessionalExperiencesID = Guid.NewGuid(),
			EmailInvitationID = EmailId,
			Emp1CompanyName = "Accenture Philippines",
			Emp1CurrentlyEmployed = false,
			Emp1PermissionToContact = true,
			Emp1CompanyCity = "Taguig",
			Emp1CompanyProvince = "Metro Manila",
			Emp1CompanyCountry = "Philippines",
			Emp1CompanyPostalCode = "1630",
			Emp1StartDate = sampleDate,
			Emp1EndDate = sampleDate,
			Emp1JobTitle = "Software Engineer",
			Emp1SupervisorName = "Maria Santos",
			Emp1SupervisorContactNumber = "+639171234567",
			Emp1COEUploadFile = CreateFakeFormFile(sampleFileContent, "coe.txt"),
			Emp1COEUploadFileName = "coe.txt",
			Emp2CompanyName = "Globe Telecom",
			Emp2CurrentlyEmployed = false,
			Emp2PermissionToContact = true,
			Emp2CompanyCity = "Makati",
			Emp2CompanyProvince = "Metro Manila",
			Emp2CompanyCountry = "Philippines",
			Emp2CompanyPostalCode = "1200",
			Emp2StartDate = sampleDate,
			Emp2EndDate = sampleDate,
			Emp2JobTitle = "Senior Backend Developer",
			Emp2SupervisorName = "Carlos Reyes",
			Emp2SupervisorContactNumber = "+639189876543",
			Emp2COEUploadFile = CreateFakeFormFile(sampleFileContent, "coe.txt"),
			Emp2COEUploadFileName = "coe.txt",
			Emp3CompanyName = "Tech Innovators Inc.",
			Emp3CurrentlyEmployed = true,
			Emp3PermissionToContact = true,
			Emp3CompanyCity = "Pasig",
			Emp3CompanyProvince = "Metro Manila",
			Emp3CompanyCountry = "Philippines",
			Emp3CompanyPostalCode = "1605",
			Emp3StartDate = sampleDate,
			Emp3EndDate = sampleDate,
			Emp3JobTitle = "Lead .NET Developer",
			Emp3SupervisorName = "Ana Lopez",
			Emp3SupervisorContactNumber = "+639155551234",
			Emp3COEUploadFile = CreateFakeFormFile(sampleFileContent, "coe.txt"),
			Emp3COEUploadFileName = "coe.txt", 
			CreatedDate = DateTime.UtcNow,
		};

		var reference = new ReferenceDetailsDTO
		{
			ReferenceDetailsID = Guid.NewGuid(),
			EmailInvitationID = EmailId,

			Ref1FullName = "Michael Tan",
			Ref1ProfessionalRelationship = "Former Team Lead",
			Ref1AffiliatedCompany = "Accenture Philippines",
			Ref1Email = "michael.tan@accenture.com",
			Ref1ContactNumber = "+639171111111",
			Ref1ModeOfContact = "Email",
			Ref1BestTimeToContact = DateTime.UtcNow,

			Ref2FullName = "Sarah Lim",
			Ref2ProfessionalRelationship = "Project Manager",
			Ref2AffiliatedCompany = "Globe Telecom",
			Ref2Email = "sarah.lim@globe.com.ph",
			Ref2ContactNumber = "+639172222222",
			Ref2ModeOfContact = "Phone",
			Ref2BestTimeToContact = DateTime.UtcNow,

			Ref3FullName = "John Bautista",
			Ref3ProfessionalRelationship = "Engineering Director",
			Ref3AffiliatedCompany = "Tech Innovators Inc.",
			Ref3Email = "john.bautista@techinnovators.com",
			Ref3ContactNumber = "+639173333333",
			Ref3ModeOfContact = "Email",
			Ref3BestTimeToContact = DateTime.UtcNow,

			CreatedDate = DateTime.UtcNow
		};

		var command = new AddApplicationFormDataCommand(
			PersonalDetails: null!,
			AddressDetails: address,
			EducationalBackground: education,
			LicensesDetails: licenses,
			ProfessionalExperiences: experiences,
			ReferenceDetails: reference
		);

		// Act & Assert
		await Assert.ThrowsAsync<NullReferenceException>(() =>
			_sender.Send(command));
	}

	private async Task SeedEmailInvitationRequestData()
	{
		var emailInvitationRequest = new EmailInvitationRequest
		{
			EmailInvitationID = EmailId,
			LastName = "Dela Cruz",
			FirstName = "Juan",
			MiddleInitial = "S",
			EmailAddress = "jsdelacruz@cibi.com.ph",
			MobileNumber = "+639171234567",
			HashTokenCreated = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(7)
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitationRequest);
		await _dbContext.SaveChangesAsync();
	}
}