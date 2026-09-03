using ATS.Data.Entities;
using ATS.DTO;
using ATS.Features.Web.AddApplicationFormData;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class AddApplicationFormDataIntegrationTests : BaseIntegrationTest
{
	/// <summary>
	/// The token seeded alongside the invitation. Submissions authorize against this,
	/// not against the EmailInvitationID carried on the DTOs.
	/// </summary>
	private const string SeededHashToken = "Hashtoken";

	private readonly string _atsTestFolder;
	private readonly Guid EmailId;
	private readonly byte[] _sampleFileContent;
	private readonly DateOnly _sampleDate;
	private readonly string _govermentIdFileName;
	private readonly string _nbiFileName;
	private readonly string _resumeFileName;
	private readonly string _highSchoolDiplomaFileName;
	private readonly string _seniorHighSchoolDiplomaFileName;
	private readonly string _bachelorDiplomaFileName;
	private readonly string _masterDiplomaFileName;
	private readonly string _doctorateDiplomaFileName;
	private readonly string _licenseFileName;
	private readonly string _emp1COEFileName;
	private readonly string _emp2COEFileName;
	private readonly string _emp3COEFileName;
	private readonly string _signatureFileName;

	public AddApplicationFormDataIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_atsTestFolder = _configuration
								.GetSection("AlibabaOss")
								.GetValue<string>("ATSTestFolder", "");

		// Initialize file content using the assembly location to find TestFiles
		// Assembly is at: D:\GitHub\1CibiPlatform\Test\Test\bin\Debug\net10.0\Test.dll
		// TestFiles is at: D:\GitHub\1CibiPlatform\Test\Test\BackendAPI\Modules\ATS.IntegrationTests\TestFiles
		var assemblyLocation = Assembly.GetExecutingAssembly().Location;
		var assemblyDir = Path.GetDirectoryName(assemblyLocation); // bin\Debug\net10.0
		// Navigate up: bin\Debug\net10.0 -> bin -> Test -> Test (3 levels up) -> BackendAPI\Modules\ATS.IntegrationTests\TestFiles
		var projectSourceDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "BackendAPI", "Modules", "ATS.IntegrationTests"));
		var testFilesPath = Path.Combine(projectSourceDir, "TestFiles", "signature.png");
		_sampleFileContent = File.ReadAllBytes(testFilesPath);

		EmailId = Guid.CreateVersion7();
		_sampleDate = DateOnly.FromDateTime(DateTime.UtcNow);
		_govermentIdFileName = $"{Guid.CreateVersion7()}-govId.pdf";
		_nbiFileName = $"{Guid.CreateVersion7()}-nbiId.pdf";
		_resumeFileName = $"{Guid.CreateVersion7()}-resume.pdf";
		_highSchoolDiplomaFileName = $"{Guid.CreateVersion7()}-highSchoolDiploma.pdf";
		_seniorHighSchoolDiplomaFileName = $"{Guid.CreateVersion7()}-seniorHighSchoolDiploma.pdf";
		_bachelorDiplomaFileName = $"{Guid.CreateVersion7()}-bachelorDiploma.pdf";
		_masterDiplomaFileName = $"{Guid.CreateVersion7()}-masterDiploma.pdf";
		_doctorateDiplomaFileName = $"{Guid.CreateVersion7()}-doctorateDiploma.pdf";
		_licenseFileName = $"{Guid.CreateVersion7()}-license.pdf";
		_emp1COEFileName = $"{Guid.CreateVersion7()}-emp1COE.pdf";
		_emp2COEFileName = $"{Guid.CreateVersion7()}-emp2COE.pdf";
		_emp3COEFileName = $"{Guid.CreateVersion7()}-emp3COE.pdf";
		_signatureFileName = $"{Guid.CreateVersion7()}-signature.txt";
	}

	private byte[] CreatePdfBytes()
	{
		// Minimal valid PDF header
		return System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%EOF");
	}

	private IFormFile CreateFakeFormFile(
	byte[] content,
	string fileName,
	string contentType = "application/octet-stream")
	{
		var stream = new MemoryStream(content);

		return new FormFile(stream, 0, content.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = contentType
		};
	}

	private async Task SeedEmailInvitationRequestData(
		string hashToken = SeededHashToken,
		string applicationFormStatus = "Pending",
		DateTime? hashTokenExpiration = null)
	{
		var emailInvitationRequest = new EmailInvitationRequest
		{
			EmailInvitationID = EmailId,
			LastName = "Dela Cruz",
			FirstName = "Juan",
			MiddleInitial = "S",
			EmailAddress = "jsdelacruz@cibi.com.ph",
			MobileNumber = "09171234567",
			PackageId = DefaultPackageId,
			SelectPackage = "Air BnB",
			RushNormal = "Rush",
			HashToken = hashToken,
			ApplicationFormStatus = applicationFormStatus,
			EmailSentStatus = "Pending",
			OrderStatus = "Pending Candidate Info",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = hashTokenExpiration ?? DateTime.UtcNow.AddDays(7)
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitationRequest);
		await _dbContext.SaveChangesAsync();
	}

	/// <summary>
	/// A minimal but valid command. The authorization tests below only care about the
	/// token, so the payload just has to survive FluentValidation.
	/// </summary>
	private AddApplicationFormDataCommand BuildValidCommand(string hashToken, Guid? claimedEmailId = null)
	{
		var emailId = claimedEmailId ?? EmailId;
		var pdfContent = CreatePdfBytes();

		var personal = new PersonalDetailsDTO
		{
			EmailInvitationID = emailId,
			PositionAppliedFor = "Senior Software Engineer",
			FirstName = "Juan",
			LastName = "Dela Cruz",
			Suffix = "Jr.",
			Sex = "Male",
			DOB = _sampleDate,
			MobileNumber = "09171234567",
			EmailAlternative = "juan.delacruz@gmail.com",
			AdditionalGovtIDFile = CreateFakeFormFile(pdfContent, _govermentIdFileName, "application/pdf"),
			AdditionalGovtIDFileName = _govermentIdFileName,
			NBIClearanceFile = CreateFakeFormFile(pdfContent, _nbiFileName, "application/pdf"),
			NBIClearanceFileName = _nbiFileName,
			ResumeFile = CreateFakeFormFile(pdfContent, _resumeFileName, "application/pdf"),
			ResumeFileName = _resumeFileName,
			CreatedDate = DateTime.UtcNow
		};

		var address = new AddressDetailsDTO
		{
			EmailInvitationID = emailId,
			CurrentAddress = "123 Mabini St., Brgy. San Isidro",
			CurrentCity = "Makati",
			CurrentProvince = "Metro Manila",
			CurrentCountry = "Philippines",
			CurrentPostalCode = "1200",
			PermanentAddress = "123 Mabini St., Brgy. San Isidro",
			PermanentCity = "Makati",
			PermanentProvince = "Metro Manila",
			PermanentCountry = "Philippines",
			PermanentPostalCode = "1200",
			TypeOfOwnership = "Owned",
			CreatedDate = DateTime.UtcNow
		};

		var education = new EducationalBackgroundDTO
		{
			EmailInvitationID = emailId,
			HighestEducationalAttainment = "College Graduate",
			BachelorsSchoolName = "University of the Philippines",
			BachelorsDegree = "BS Computer Science",
			BachelorsGraduationDate = _sampleDate,
			BachelorsDiplomaFile = CreateFakeFormFile(pdfContent, _bachelorDiplomaFileName, "application/pdf"),
			BachelorsDiplomaFileName = _bachelorDiplomaFileName,
			CreatedDate = DateTime.UtcNow
		};

		var licenses = new LicensesDetailsDTO
		{
			EmailInvitationID = emailId,
			CreatedDate = DateTime.UtcNow
		};

		var experiences = new ProfessionalExperiencesDTO
		{
			EmailInvitationID = emailId,
			Emp1CompanyName = "Cibi Information Inc.",
			Emp1CompanyCity = "Makati",
			Emp1CompanyProvince = "Metro Manila",
			Emp1CompanyCountry = "Philippines",
			Emp1CompanyPostalCode = "1200",
			Emp1DatePermittedToContact = _sampleDate,
			Emp1JobTitle = "Software Engineer",
			Emp1StartDate = _sampleDate,
			Emp1EndDate = _sampleDate,
			Emp1SupervisorName = "Maria Cruz",
			Emp1SupervisorContactNumber = "09171234567",
			Emp1COEUploadFile = CreateFakeFormFile(pdfContent, _emp1COEFileName, "application/pdf"),
			Emp1COEUploadFileName = _emp1COEFileName,
			CreatedDate = DateTime.UtcNow
		};

		var reference = new ReferenceDetailsDTO
		{
			EmailInvitationID = emailId,
			Ref1FullName = "Michael Tan",
			Ref1ProfessionalRelationship = "Former Team Lead",
			Ref1AffiliatedCompany = "Accenture Philippines",
			Ref1Email = "michael.tan@accenture.com",
			Ref1ContactNumber = "09171234567",
			Ref1ModeOfContact = "Email",
			Ref1BestTimeToContact = DateTime.UtcNow,
			Ref2FullName = "Sarah Lim",
			Ref2ProfessionalRelationship = "Project Manager",
			Ref2AffiliatedCompany = "Globe Telecom",
			Ref2Email = "sarah.lim@globe.com.ph",
			Ref2ContactNumber = "09171234567",
			Ref2ModeOfContact = "Phone",
			Ref2BestTimeToContact = DateTime.UtcNow,
			CreatedDate = DateTime.UtcNow
		};

		var signature = new SignatureDetailsDTO
		{
			EmailInvitationID = emailId,
			Signature = CreateFakeFormFile(_sampleFileContent, "signature.png", "image/png"),
			SignerName = "Juan S. Dela Cruz",
			SignatureDate = _sampleDate
		};

		return new AddApplicationFormDataCommand(
			hashToken, personal, address, education, licenses, experiences, reference, signature);
	}

	#region Positive Path
	[Fact]
	public async Task AddApplicationFormData_WithSamplePayload_ShouldReturnTrue()
	{
		await SeedEmailInvitationRequestData();

		var pdfContent = CreatePdfBytes();

		var personal = new PersonalDetailsDTO
		{
			EmailInvitationID = EmailId,
			PositionAppliedFor = "Senior Software Engineer",
			FirstName = "Juan",
			MiddleName = "Santos",
			LastName = "Dela Cruz",
			Suffix = "Jr.",
			Sex = "Male",
			DOB = _sampleDate,
			MobileNumber = "09171234567",
			EmailAlternative = "juan.delacruz@gmail.com",
			AdditionalGovtIDFile = CreateFakeFormFile(pdfContent, _govermentIdFileName, "application/pdf"),
			AdditionalGovtIDFileName = _govermentIdFileName,
			NBIClearanceFile = CreateFakeFormFile(pdfContent, _nbiFileName, "application/pdf"),
			NBIClearanceFileName = _nbiFileName,
			ResumeFile = CreateFakeFormFile(pdfContent, _resumeFileName, "application/pdf"),
			ResumeFileName = _resumeFileName,
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
			HighSchoolGraduationDate = _sampleDate,
			HighSchoolDiplomaFile = CreateFakeFormFile(pdfContent, _highSchoolDiplomaFileName, "application/pdf"),
			HighSchoolDiplomaFileName = _highSchoolDiplomaFileName,
			SeniorHighSchoolName = "UST Senior High School",
			SeniorHighSchoolGraduationDate = _sampleDate,
			SeniorHighSchoolDiplomaFile = CreateFakeFormFile(pdfContent, _seniorHighSchoolDiplomaFileName, "application/pdf"),
			SeniorHighSchoolDiplomaFileName = _highSchoolDiplomaFileName,
			BachelorsSchoolName = "University of Santo Tomas",
			BachelorsGraduationDate = _sampleDate,
			BachelorsDiplomaFile = CreateFakeFormFile(pdfContent, _bachelorDiplomaFileName, "application/pdf"),
			BachelorsDiplomaFileName = _bachelorDiplomaFileName,
			BachelorsDegree = "Computer Science",
			MastersSchoolName = "Ateneo de Manila University",
			MastersGraduationDate = _sampleDate,
			MastersDiplomaFile = CreateFakeFormFile(pdfContent, _masterDiplomaFileName, "application/pdf"),
			MastersDiplomaFileName = _masterDiplomaFileName,
			MastersDegree = "Information Technology",
			PhDSchoolName = string.Empty,
			DoctorateGraduationDate = _sampleDate,
			DoctorateDiplomaFile = CreateFakeFormFile(pdfContent, _doctorateDiplomaFileName, "application/pdf"),
			DoctorateDiplomaFileName = _doctorateDiplomaFileName,
			DoctorateDegree = string.Empty,
			CreatedDate = DateTime.UtcNow,
		};

		var licenses = new LicensesDetailsDTO
		{
			EmailInvitationID = EmailId,
			LicenseName = "AWS Certified Developer",
			LicenseNumber = "AWS-DEV-2026-001",
			LicenseExpiryDate = _sampleDate,
			LicenseUploadFile = CreateFakeFormFile(pdfContent, _licenseFileName, "application/pdf"),
			LicenseUploadFileName = _licenseFileName,
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
			Emp1StartDate = _sampleDate,
			Emp1EndDate = _sampleDate,
			Emp1JobTitle = "Software Engineer",
			Emp1SupervisorName = "Maria Santos",
			Emp1SupervisorContactNumber = "09171234567",
			Emp1COEUploadFile = CreateFakeFormFile(pdfContent, _emp1COEFileName, "application/pdf"),
			Emp1COEUploadFileName = _emp1COEFileName,
			Emp1DatePermittedToContact = _sampleDate,
			Emp2CompanyName = "Globe Telecom",
			Emp2CurrentlyEmployed = false,
			Emp2PermissionToContact = true,
			Emp2CompanyCity = "Makati",
			Emp2CompanyProvince = "Metro Manila",
			Emp2CompanyCountry = "Philippines",
			Emp2CompanyPostalCode = "1200",
			Emp2StartDate = _sampleDate,
			Emp2EndDate = _sampleDate,
			Emp2JobTitle = "Senior Backend Developer",
			Emp2SupervisorName = "Carlos Reyes",
			Emp2SupervisorContactNumber = "09171234567",
			Emp2COEUploadFile = CreateFakeFormFile(pdfContent, _emp2COEFileName, "application/pdf"),
			Emp2COEUploadFileName = _emp2COEFileName,
			Emp2DatePermittedToContact = _sampleDate,
			Emp3CompanyName = "Tech Innovators Inc.",
			Emp3CurrentlyEmployed = true,
			Emp3PermissionToContact = true,
			Emp3CompanyCity = "Pasig",
			Emp3CompanyProvince = "Metro Manila",
			Emp3CompanyCountry = "Philippines",
			Emp3CompanyPostalCode = "1605",
			Emp3StartDate = _sampleDate,
			Emp3EndDate = _sampleDate,
			Emp3JobTitle = "Lead .NET Developer",
			Emp3SupervisorName = "Ana Lopez",
			Emp3SupervisorContactNumber = "09171234567",
			Emp3COEUploadFile = CreateFakeFormFile(pdfContent, _emp3COEFileName, "application/pdf"),
			Emp3COEUploadFileName = _emp3COEFileName,
			Emp3DatePermittedToContact = _sampleDate,
			CreatedDate = DateTime.UtcNow,
		};

		var reference = new ReferenceDetailsDTO
		{
			EmailInvitationID = EmailId,
			Ref1FullName = "Michael Tan",
			Ref1ProfessionalRelationship = "Former Team Lead",
			Ref1AffiliatedCompany = "Accenture Philippines",
			Ref1Email = "michael.tan@accenture.com",
			Ref1ContactNumber = "09171234567",
			Ref1ModeOfContact = "Email",
			Ref1BestTimeToContact = DateTime.UtcNow,
			Ref2FullName = "Sarah Lim",
			Ref2ProfessionalRelationship = "Project Manager",
			Ref2AffiliatedCompany = "Globe Telecom",
			Ref2Email = "sarah.lim@globe.com.ph",
			Ref2ContactNumber = "09171234567",
			Ref2ModeOfContact = "Phone",
			Ref2BestTimeToContact = DateTime.UtcNow,
			Ref3FullName = "John Bautista",
			Ref3ProfessionalRelationship = "Engineering Director",
			Ref3AffiliatedCompany = "Tech Innovators Inc.",
			Ref3Email = "john.bautista@techinnovators.com",
			Ref3ContactNumber = "09171234567",
			Ref3ModeOfContact = "Email",
			Ref3BestTimeToContact = DateTime.UtcNow,
			CreatedDate = DateTime.UtcNow
		};

		var signature = new SignatureDetailsDTO
		{
			EmailInvitationID = EmailId,
			Signature = CreateFakeFormFile(_sampleFileContent,"signature.png","image/png"),
			SignerName = "Juan S. Dela Cruz",
			SignatureDate = _sampleDate
		};

		var command = new AddApplicationFormDataCommand(SeededHashToken, personal, address, education, licenses, experiences, reference, signature);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.IsAdded.Should().BeTrue();

		var consentFile = _dbContext.SignatureDetails.FirstOrDefault(e => e.EmailInvitationID == EmailId);

		if (result.IsAdded == true)
		{
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_govermentIdFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_nbiFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_resumeFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_highSchoolDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_seniorHighSchoolDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_bachelorDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_masterDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_doctorateDiplomaFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_licenseFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_emp1COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_emp2COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_emp3COEFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{_signatureFileName}");
			await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{consentFile!.ConsentFormFileName}");
		}
	}
	#endregion

	#region Negative Path
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
			HighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, _highSchoolDiplomaFileName),
			HighSchoolDiplomaFileName = _highSchoolDiplomaFileName,
			SeniorHighSchoolName = "UST Senior High School",
			SeniorHighSchoolGraduationDate = sampleDate,
			SeniorHighSchoolDiplomaFile = CreateFakeFormFile(sampleFileContent, _seniorHighSchoolDiplomaFileName),
			SeniorHighSchoolDiplomaFileName = _seniorHighSchoolDiplomaFileName,
			BachelorsSchoolName = "University of Santo Tomas",
			BachelorsGraduationDate = sampleDate,
			BachelorsDiplomaFile = CreateFakeFormFile(sampleFileContent, _bachelorDiplomaFileName),
			BachelorsDiplomaFileName = _bachelorDiplomaFileName,
			BachelorsDegree = "Computer Science",
			MastersSchoolName = "Ateneo de Manila University",
			MastersGraduationDate = sampleDate,
			MastersDiplomaFile = CreateFakeFormFile(sampleFileContent, _masterDiplomaFileName),
			MastersDiplomaFileName = _masterDiplomaFileName,
			MastersDegree = "Information Technology",
			PhDSchoolName = string.Empty,
			DoctorateGraduationDate = sampleDate,
			DoctorateDiplomaFile = CreateFakeFormFile(Array.Empty<byte>(), _doctorateDiplomaFileName),
			DoctorateDiplomaFileName = _doctorateDiplomaFileName,
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
			Emp1SupervisorContactNumber = "09171234567",
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
			Emp2SupervisorContactNumber = "09171234567",
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
			Emp3SupervisorContactNumber = "09171234567",
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
			Ref1ContactNumber = "09171234567",
			Ref1ModeOfContact = "Email",
			Ref1BestTimeToContact = DateTime.UtcNow,

			Ref2FullName = "Sarah Lim",
			Ref2ProfessionalRelationship = "Project Manager",
			Ref2AffiliatedCompany = "Globe Telecom",
			Ref2Email = "sarah.lim@globe.com.ph",
			Ref2ContactNumber = "09171234567",
			Ref2ModeOfContact = "Phone",
			Ref2BestTimeToContact = DateTime.UtcNow,

			Ref3FullName = "John Bautista",
			Ref3ProfessionalRelationship = "Engineering Director",
			Ref3AffiliatedCompany = "Tech Innovators Inc.",
			Ref3Email = "john.bautista@techinnovators.com",
			Ref3ContactNumber = "09171234567",
			Ref3ModeOfContact = "Email",
			Ref3BestTimeToContact = DateTime.UtcNow,

			CreatedDate = DateTime.UtcNow
		};

		var signature = new SignatureDetailsDTO
		{
			EmailInvitationID = EmailId,
			Signature = CreateFakeFormFile(sampleFileContent, "signature.txt"),
		};


		var command = new AddApplicationFormDataCommand(SeededHashToken, null!, address, education, licenses, experiences, reference, signature);

		// Act & Assert
		await Assert.ThrowsAsync<ValidationException>(() =>
			_sender.Send(command));
	}
	#endregion

	#region Hash Token Authorization
	// The endpoint is anonymous by design - candidates arrive from an emailed link with
	// no account - so the hash token is the entire authorization decision. These cover
	// the ways a caller can try to get around it.

	[Fact]
	public async Task AddApplicationFormData_WithUnknownHashToken_ShouldThrowNotFound()
	{
		await SeedEmailInvitationRequestData();

		var command = BuildValidCommand("not-a-real-token");

		await Assert.ThrowsAsync<NotFoundException>(() => _sender.Send(command));

		// Nothing may be written against the invitation the caller aimed at.
		_dbContext.PersonalDetails
			.Any(p => p.EmailInvitationID == EmailId)
			.Should().BeFalse();
	}

	[Fact]
	public async Task AddApplicationFormData_WithEmptyHashToken_ShouldThrowValidation()
	{
		await SeedEmailInvitationRequestData();

		var command = BuildValidCommand(string.Empty);

		await Assert.ThrowsAsync<ValidationException>(() => _sender.Send(command));
	}

	[Fact]
	public async Task AddApplicationFormData_WithExpiredHashToken_ShouldThrowBadRequest()
	{
		await SeedEmailInvitationRequestData(
			hashTokenExpiration: DateTime.UtcNow.AddDays(-1));

		var command = BuildValidCommand(SeededHashToken);

		await Assert.ThrowsAsync<BadRequestException>(() => _sender.Send(command));

		_dbContext.PersonalDetails
			.Any(p => p.EmailInvitationID == EmailId)
			.Should().BeFalse();
	}

	[Fact]
	public async Task AddApplicationFormData_WhenFormAlreadySubmitted_ShouldThrowConflict()
	{
		await SeedEmailInvitationRequestData(applicationFormStatus: "Done");

		var command = BuildValidCommand(SeededHashToken);

		// Without the status check this surfaced as an opaque 500 from the
		// PersonalDetails 1:1 unique constraint instead of a 409.
		await Assert.ThrowsAsync<ConflictException>(() => _sender.Send(command));
	}

	[Fact]
	public async Task AddApplicationFormData_WhenFormWithdrawn_ShouldThrowConflict()
	{
		await SeedEmailInvitationRequestData(applicationFormStatus: "Withdrawn");

		var command = BuildValidCommand(SeededHashToken);

		await Assert.ThrowsAsync<ConflictException>(() => _sender.Send(command));
	}

	[Fact]
	public async Task AddApplicationFormData_WithMismatchedEmailInvitationId_ShouldBindToTokenOwner()
	{
		// The heart of the finding: a caller posts a valid token of their own but
		// substitutes somebody else's EmailInvitationID in the body. The body value must
		// be ignored entirely.
		await SeedEmailInvitationRequestData();

		var victimEmailId = Guid.CreateVersion7();
		var command = BuildValidCommand(SeededHashToken, claimedEmailId: victimEmailId);

		var result = await _sender.Send(command);

		result.IsAdded.Should().BeTrue();

		// Written against the token's own invitation...
		_dbContext.PersonalDetails
			.Any(p => p.EmailInvitationID == EmailId)
			.Should().BeTrue();

		// ...and not against the id the caller asked for.
		_dbContext.PersonalDetails
			.Any(p => p.EmailInvitationID == victimEmailId)
			.Should().BeFalse();

		await CleanUpUploadedTestFilesAsync();
	}

	private async Task CleanUpUploadedTestFilesAsync()
	{
		foreach (var fileName in new[]
		{
			_govermentIdFileName,
			_nbiFileName,
			_resumeFileName,
			_bachelorDiplomaFileName,
			_emp1COEFileName
		})
		{
			try
			{
				await _objectStorageService.DeleteAsync($"{_atsTestFolder}/{fileName}");
			}
			catch
			{
				// Best effort - a leftover test object must not fail the assertion above.
			}
		}
	}
	#endregion


}