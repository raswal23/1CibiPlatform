using ATS.DTO;
using ATS.Services.FilePDFService;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

// QuestPDF resolves the whole layout at generation time and throws on layout
// errors, so generating against a full and an empty preview is a real check
// that the document composes for both extremes - not just a smoke test.
public class FilePdfServiceTests
{
	private readonly FilePdfService _filePdfService = new();

	[Fact]
	public async Task GenerateApplicationFormPreviewPdfAsync_ShouldProducePdf_ForFullyPopulatedPreview()
	{
		var preview = new ApplicationFormPreviewDTO
		{
			SubjectName = "Juan Dela Cruz",
			FilledFormAt = "September 14, 2026",
			PhilSysVerified = true,
			Personal = new PersonalPreviewDTO
			{
				PositionAppliedFor = "Software Engineer",
				FirstName = "Juan",
				MiddleName = "Santos",
				LastName = "Dela Cruz",
				Sex = "Male",
				DateOfBirth = "January 01, 1990",
				MaritalStatus = "Single",
				Nationality = "Filipino",
				MobileNumber = "09171234567",
				EmailAddress = "juan@example.com",
				SSS = "01-2345678-9",
				TIN = "123-456-789",
				GovtIdFileName = "govtid.pdf",
				NbiClearanceFileName = "nbi.pdf",
				ResumeFileName = "resume.pdf"
			},
			Address = new AddressPreviewDTO
			{
				CurrentAddress = "123 Main St",
				CurrentCity = "Makati",
				CurrentProvince = "Metro Manila",
				CurrentCountry = "Philippines",
				CurrentPostalCode = "1200",
				CurrentTypeOfOwnership = "Rented",
				PermanentAddress = "456 Home Ave",
				PermanentCity = "Cebu City",
				PermanentProvince = "Cebu",
				PermanentCountry = "Philippines",
				PermanentPostalCode = "6000"
			},
			Education = new EducationPreviewDTO
			{
				HighestEducationalAttainment = "Bachelor's Degree",
				SchoolName = "University of the Philippines",
				Degree = "BS Computer Science",
				GraduationDate = "April 2012",
				DiplomaFileName = "diploma.pdf"
			},
			License = new LicensePreviewDTO
			{
				LicenseName = "PRC License",
				LicenseNumber = "1234567",
				LicenseExpiryDate = "December 31, 2027",
				LicenseFileName = "license.pdf"
			},
			Employers =
			[
				new EmployerPreviewDTO
				{
					CompanyName = "Acme Corp",
					JobTitle = "Developer",
					CompanyAddress = "BGC, Taguig",
					StartDate = "June 2015",
					EndDate = "May 2020",
					CurrentlyEmployed = "No",
					PermissionToContact = "Yes",
					SupervisorName = "Maria Reyes",
					SupervisorContactNumber = "09181234567",
					SupervisorEmail = "maria@acme.com",
					CoeFileName = "coe.pdf"
				},
				new EmployerPreviewDTO
				{
					CompanyName = "Beta Inc",
					JobTitle = "Senior Developer",
					StartDate = "June 2020",
					CurrentlyEmployed = "Yes"
				}
			],
			References =
			[
				new ReferencePreviewDTO
				{
					FullName = "Pedro Penduko",
					ProfessionalRelationship = "Former manager",
					AffiliatedCompany = "Acme Corp",
					Email = "pedro@acme.com",
					ContactNumber = "09191234567",
					ModeOfContact = "Email",
					BestTimeToContact = "Mornings"
				}
			],
			Signature = new SignaturePreviewDTO
			{
				SignerName = "Juan Dela Cruz",
				SignatureDate = "September 14, 2026",
				ConsentFormFileName = "consent.pdf"
			}
		};

		var pdf = await _filePdfService.GenerateApplicationFormPreviewPdfAsync(preview);

		AssertIsPdf(pdf);
	}

	[Fact]
	public async Task GenerateApplicationFormPreviewPdfAsync_ShouldProducePdf_ForEmptyPreview()
	{
		var preview = new ApplicationFormPreviewDTO();

		var pdf = await _filePdfService.GenerateApplicationFormPreviewPdfAsync(preview);

		AssertIsPdf(pdf);
	}

	private static void AssertIsPdf(MemoryStream pdf)
	{
		pdf.Length.Should().BeGreaterThan(0);
		pdf.Position.Should().Be(0);

		Span<byte> magic = stackalloc byte[4];
		pdf.ReadExactly(magic);
		System.Text.Encoding.ASCII.GetString(magic).Should().Be("%PDF");
	}
}
