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
	Guid EmailId = Guid.CreateVersion7();
	byte[] sampleFileContent = Convert.FromBase64String("SGVsbG8gV29ybGQ=");
	DateOnly sampleDate = DateOnly.FromDateTime(DateTime.UtcNow);
	string govermentIdFileName = $"{Guid.CreateVersion7()}-govId.txt";
	string nbiFileName = $"{Guid.CreateVersion7()}-nbiId.txt";
	string resumeFileName = $"{Guid.CreateVersion7()}-govId.txt";
	string highSchoolDiplomaFileName = $"{Guid.CreateVersion7()}-highSchoolDiploma.txt";
	string seniorHighSchoolDiplomaFileName = $"{Guid.CreateVersion7()}-seniorHighSchoolDiplomaFileName.txt";
	string bachelorDiplomaFileName = $"{Guid.CreateVersion7()}-bachelorSchoolDiplomaFileName.txt";
	string masterDiplomaFileName = $"{Guid.CreateVersion7()}-masterSchoolDiplomaFileName.txt";
	string doctorateDiplomaFileName = $"{Guid.CreateVersion7()}-doctorateSchoolDiplomaFileName.txt";
	string licenseFileName = $"{Guid.CreateVersion7()}-licenseFileName.txt";
	string emp1COEFileName = $"{Guid.CreateVersion7()}-emp1COEFileName.txt";
	string emp2COEFileName = $"{Guid.CreateVersion7()}-emp2COEFileName.txt";
	string emp3COEFileName = $"{Guid.CreateVersion7()}-emp3COEFileName.txt";
	string signatureFileName = $"{Guid.CreateVersion7()}-signature.txt";

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

		var personal = new PersonalDetailsDTO
		{
			EmailInvitationID = EmailId,
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
			BachelorsDiplomaFile = CreateFakeFormFile(sampleFileContent, bachelorDiplomaFileName),
			BachelorsDiplomaFileName = bachelorDiplomaFileName,
			BachelorsDegree = "Computer Science",
			MastersSchoolName = "Ateneo de Manila University",
			MastersGraduationDate = sampleDate,
			MastersDiplomaFile = CreateFakeFormFile(sampleFileContent, masterDiplomaFileName),
			MastersDiplomaFileName = masterDiplomaFileName,
			MastersDegree = "Information Technology",
			PhDSchoolName = string.Empty,
			DoctorateGraduationDate = sampleDate,
			DoctorateDiplomaFile = CreateFakeFormFile(sampleFileContent, doctorateDiplomaFileName),
			DoctorateDiplomaFileName = doctorateDiplomaFileName,
			DoctorateDegree = string.Empty,
			CreatedDate = DateTime.UtcNow,
		};

		var licenses = new LicensesDetailsDTO
		{
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

		var signature = new SignatureDetailsDTO
		{
			EmailInvitationID = EmailId,
			Signature = CreateFakeFormFile(sampleFileContent, signatureFileName),
		};

		var command = new AddApplicationFormDataCommand(personal, address, education, licenses, experiences, reference, signature);

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
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{bachelorDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{masterDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{doctorateDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{licenseFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{emp1COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{emp2COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{emp3COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{signatureFileName}");
		}
	}

	[Fact]
	public async Task AddApplicationFormData_MissingPersonal_ShouldThrowNullReferenceException()
	{
		await SeedEmailInvitationRequestData();

		byte[] sampleFileContent = Convert.FromBase64String("SGVsbG8gV29ybGQ=");
		DateOnly sampleDate = DateOnly.FromDateTime(DateTime.UtcNow);

		var address = new AddressDetailsDTO
		{
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
			EmailInvitationID = EmailId,
			HighestEducationalAttainment = "Bachelor's Degree",
			HighSchoolName = "Manila Science High School",
			HighSchoolGraduationDate = sampleDate,
			HighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, highSchoolDiplomaFileName),
			HighSchoolDiplomaFileName = highSchoolDiplomaFileName,
			SeniorHighSchoolName = "UST Senior High School",
			SeniorHighSchoolGraduationDate = sampleDate,
			SeniorHighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, seniorHighSchoolDiplomaFileName),
			SeniorHighSchoolDiplomaFileName = seniorHighSchoolDiplomaFileName,
			BachelorsSchoolName = "University of Santo Tomas",
			BachelorsGraduationDate = sampleDate,
			BachelorsDiplomaFile = CreateFakeFormFile(sampleFileContent, bachelorDiplomaFileName),
			BachelorsDiplomaFileName = bachelorDiplomaFileName,
			BachelorsDegree = "Computer Science",
			MastersSchoolName = "Ateneo de Manila University",
			MastersGraduationDate = sampleDate,
			MastersDiplomaFile = CreateFakeFormFile(sampleFileContent, masterDiplomaFileName),
			MastersDiplomaFileName = masterDiplomaFileName,
			MastersDegree = "Information Technology",
			PhDSchoolName = string.Empty,
			DoctorateGraduationDate = sampleDate,
			DoctorateDiplomaFile = CreateFakeFormFile(Array.Empty<byte>(), doctorateDiplomaFileName),
			DoctorateDiplomaFileName = doctorateDiplomaFileName,
			DoctorateDegree = string.Empty,
			CreatedDate = DateTime.UtcNow,
		};

		var licenses = new LicensesDetailsDTO
		{
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

		var signature = new SignatureDetailsDTO
		{
			EmailInvitationID = EmailId,
			Signature = CreateFakeFormFile(sampleFileContent, "signature.txt"),
		};


		var command = new AddApplicationFormDataCommand(null!, address, education, licenses, experiences, reference, signature);

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
			HashTokenExpiration = DateTime.UtcNow.AddDays(7)
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitationRequest);
		await _dbContext.SaveChangesAsync();
	}
}