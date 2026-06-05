
namespace FrontendWebassembly.Services.ATS.Implementation;

public class ATSService : IATSService
{
    private readonly HttpClient _httpClient;

	public ATSService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<bool> AddApplicationFormDataAsync(PersonalDetailsDTO PersonalDetails, 
														AddressDetailsDTO AddressDetails, 
														EducationalBackgroundDTO EducationalBackground, 
														LicensesDetailsDTO LicensesDetails, 
														ProfessionalExperiencesDTO ProfessionalExperiences, 
														ReferenceDetailsDTO ReferenceDetails,
														SignatureDetailsDTO SignatureDetails)
	{
     using var content = new MultipartFormDataContent();

		void AddString(string? value, string name)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				content.Add(new StringContent(value), name);
			}
		}

		void AddFile(byte[]? file, string name)
		{
			if (file != null)
			{
				var stream = new MemoryStream(file);
				var fileContent = new StreamContent(stream);
				fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
				content.Add(fileContent, name, name);
			}
		}

		// PersonalDetails
		AddString(PersonalDetails.EmailInvitationID.ToString(), "PersonalDetails.EmailInvitationID");
		AddString(PersonalDetails.PersonalID.ToString(), "PersonalDetails.PersonalID");
		AddString(PersonalDetails.PositionAppliedFor, "PersonalDetails.PositionAppliedFor");
		AddString(PersonalDetails.FirstName, "PersonalDetails.FirstName");
		AddString(PersonalDetails.MiddleName, "PersonalDetails.MiddleName");
		AddString(PersonalDetails.LastName, "PersonalDetails.LastName");
		AddString(PersonalDetails.Suffix, "PersonalDetails.Suffix");
		AddString(PersonalDetails.Sex, "PersonalDetails.Sex");
		AddString(PersonalDetails.DOB?.ToString("MM-dd-yyyy"), "PersonalDetails.DOB");
		AddString(PersonalDetails.MobileNumber, "PersonalDetails.MobileNumber");	
		AddString(PersonalDetails.EmailAlternative, "PersonalDetails.EmailAlternative");
		AddFile(PersonalDetails.AdditionalGovtIDFile, "PersonalDetails.AdditionalGovtIDFile");
		AddString(PersonalDetails.AdditionalGovtIDFileName, "PersonalDetails.AdditionalGovtIDFileName");
		AddFile(PersonalDetails.NBIClearanceFile, "PersonalDetails.NBIClearanceFile");
		AddString(PersonalDetails.NBIClearanceFileName, "PersonalDetails.NBIClearanceFileName");
		AddFile(PersonalDetails.ResumeFile, "PersonalDetails.ResumeFile");
		AddString(PersonalDetails.ResumeFileName, "PersonalDetails.ResumeFileName");

		// AddressDetails
		AddString(AddressDetails.Address.ToString(), "PersonalDetaAddressDetailsils.Address");
		AddString(AddressDetails.EmailInvitationID.ToString(), "AddressDetails.EmailInvitationID");
		AddString(AddressDetails.CurrentAddress, "AddressDetails.CurrentAddress");
		AddString(AddressDetails.CurrentCity, "AddressDetails.CurrentCity");
		AddString(AddressDetails.CurrentProvince, "AddressDetails.CurrentProvince");
		AddString(AddressDetails.CurrentCountry, "AddressDetails.CurrentCountry");
		AddString(AddressDetails.CurrentPostalCode, "AddressDetails.CurrentPostalCode");
		AddString(AddressDetails.TypeOfOwnership, "AddressDetails.TypeOfOwnership");
		AddString(AddressDetails.PermanentAddress, "AddressDetails.PermanentAddress");
		AddString(AddressDetails.PermanentCity, "AddressDetails.PermanentCity");
		AddString(AddressDetails.PermanentProvince, "AddressDetails.PermanentProvince");
		AddString(AddressDetails.PermanentCountry, "AddressDetails.PermanentCountry");
		AddString(AddressDetails.PermanentPostalCode, "AddressDetails.PermanentPostalCode");

		// EducationalBackground
		AddString(EducationalBackground.EducationalBackgroundID.ToString(), "EducationalBackground.EducationalBackgroundID");
		AddString(EducationalBackground.EmailInvitationID.ToString(), "EducationalBackground.EmailInvitationID");
		AddString(EducationalBackground.HighestEducationalAttainment, "EducationalBackground.HighestEducationalAttainment");
		AddString(EducationalBackground.HighSchoolName, "EducationalBackground.HighSchoolName");
		AddString(EducationalBackground.HighSchoolGraduationDate?.ToString("yyyy-MM-dd"), "EducationalBackground.HighSchoolGraduationDate");
		AddFile(EducationalBackground.HighSchoolDiplomaFile, "EducationalBackground.HighSchoolDiplomaFile");
		AddString(EducationalBackground.HighSchoolDiplomaFileName, "EducationalBackground.HighSchoolDiplomaFileName");
		AddString(EducationalBackground.SeniorHighSchoolName, "EducationalBackground.SeniorHighSchoolName");
		AddString(EducationalBackground.SeniorHighSchoolGraduationDate?.ToString("yyyy-MM-dd"), "EducationalBackground.SeniorHighSchoolGraduationDate");
		AddFile(EducationalBackground.SeniorHighSchoolDiplomaFile, "EducationalBackground.SeniorHighSchoolDiplomaFile");
		AddString(EducationalBackground.SeniorHighSchoolDiplomaFileName, "EducationalBackground.SeniorHighSchoolDiplomaFileName");
		AddString(EducationalBackground.BachelorsSchoolName, "EducationalBackground.BachelorsSchoolName");
		AddString(EducationalBackground.BachelorsGraduationDate?.ToString("yyyy-MM-dd"), "EducationalBackground.BachelorsGraduationDate");
		AddString(EducationalBackground.BachelorsDegree, "EducationalBackground.BachelorsDegree");
		AddFile(EducationalBackground.BachelorsDiplomaFile, "EducationalBackground.BachelorsDiplomaFile");
		AddString(EducationalBackground.BachelorsDiplomaFileName, "EducationalBackground.BachelorsDiplomaFileName");
		AddString(EducationalBackground.MastersSchoolName, "EducationalBackground.MastersSchoolName");
		AddString(EducationalBackground.MastersGraduationDate?.ToString("yyyy-MM-dd"), "EducationalBackground.MastersGraduationDate");
		AddString(EducationalBackground.MastersDegree, "EducationalBackground.MastersDegree");
		AddFile(EducationalBackground.MastersDiplomaFile, "EducationalBackground.MastersDiplomaFile");
		AddString(EducationalBackground.MastersDiplomaFileName, "EducationalBackground.MastersDiplomaFileName");
		AddString(EducationalBackground.PhDSchoolName, "EducationalBackground.PhDSchoolName");
		AddString(EducationalBackground.DoctorateGraduationDate?.ToString("yyyy-MM-dd"), "EducationalBackground.DoctorateGraduationDate");
		AddString(EducationalBackground.DoctorateDegree, "EducationalBackground.DoctorateDegree");
		AddFile(EducationalBackground.DoctorateDiplomaFile, "EducationalBackground.DoctorateDiplomaFile");
		AddString(EducationalBackground.DoctorateDiplomaFileName, "EducationalBackground.DoctorateDiplomaFileName");

		// LicensesDetails
		AddString(LicensesDetails.LicensesDetailsID.ToString(), "LicensesDetails.LicensesDetailsID");
		AddString(LicensesDetails.EmailInvitationID.ToString(), "LicensesDetails.EmailInvitationID");
		AddString(LicensesDetails.LicenseName, "LicensesDetails.LicenseName");
		AddString(LicensesDetails.LicenseNumber, "LicensesDetails.LicenseNumber");
		AddString(LicensesDetails.LicenseExpiryDate?.ToString("MM-dd-yyyy"), "LicensesDetails.LicenseExpiryDate");
		AddFile(LicensesDetails.LicenseUploadFile, "LicensesDetails.LicenseUploadFile");
		AddString(LicensesDetails.LicenseUploadFileName, "LicensesDetails.LicenseUploadFileName");

		// ProfessionalExperiences
		AddString(ProfessionalExperiences.ProfessionalExperiencesID.ToString(), "ProfessionalExperiences.ProfessionalExperiencesID");
		AddString(ProfessionalExperiences.EmailInvitationID.ToString(), "ProfessionalExperiences.EmailInvitationID");
		AddString(ProfessionalExperiences.Emp1CompanyName, "ProfessionalExperiences.Emp1CompanyName");
		AddString(ProfessionalExperiences.Emp1JobTitle, "ProfessionalExperiences.Emp1JobTitle");
		AddString(ProfessionalExperiences.Emp1CurrentlyEmployed?.ToString(), "ProfessionalExperiences.Emp1CurrentlyEmployed");
		AddString(ProfessionalExperiences.Emp1PermissionToContact?.ToString(), "ProfessionalExperiences.Emp1PermissionToContact");
		AddString(ProfessionalExperiences.Emp1CompanyCity, "ProfessionalExperiences.Emp1CompanyCity");
		AddString(ProfessionalExperiences.Emp1CompanyProvince, "ProfessionalExperiences.Emp1CompanyProvince");
		AddString(ProfessionalExperiences.Emp1CompanyCountry, "ProfessionalExperiences.Emp1CompanyCountry");
		AddString(ProfessionalExperiences.Emp1CompanyPostalCode, "ProfessionalExperiences.Emp1CompanyPostalCode");
		AddString(ProfessionalExperiences.Emp1StartDate?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp1StartDate");
		AddString(ProfessionalExperiences.Emp1EndDate?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp1EndDate");
		AddString(ProfessionalExperiences.Emp1DatePermittedToContact?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp1DatePermittedToContact");
		AddString(ProfessionalExperiences.Emp1SupervisorName, "ProfessionalExperiences.Emp1SupervisorName");
		AddString(ProfessionalExperiences.Emp1SupervisorContactNumber, "ProfessionalExperiences.Emp1SupervisorContactNumber");
		AddFile(ProfessionalExperiences.Emp1COEUploadFile, "ProfessionalExperiences.Emp1COEUploadFile");
		AddString(ProfessionalExperiences.Emp1COEUploadFileName, "ProfessionalExperiences.Emp1COEUploadFileName");

		AddString(ProfessionalExperiences.Emp2CompanyName, "ProfessionalExperiences.Emp2CompanyName");
		AddString(ProfessionalExperiences.Emp2JobTitle, "ProfessionalExperiences.Emp2JobTitle");
		AddString(ProfessionalExperiences.Emp2CurrentlyEmployed?.ToString(), "ProfessionalExperiences.Emp2CurrentlyEmployed");
		AddString(ProfessionalExperiences.Emp2PermissionToContact?.ToString(), "ProfessionalExperiences.Emp2PermissionToContact");
		AddString(ProfessionalExperiences.Emp2CompanyCity, "ProfessionalExperiences.Emp2CompanyCity");
		AddString(ProfessionalExperiences.Emp2CompanyProvince, "ProfessionalExperiences.Emp2CompanyProvince");
		AddString(ProfessionalExperiences.Emp2CompanyCountry, "ProfessionalExperiences.Emp2CompanyCountry");
		AddString(ProfessionalExperiences.Emp2CompanyPostalCode, "ProfessionalExperiences.Emp2CompanyPostalCode");
		AddString(ProfessionalExperiences.Emp2StartDate?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp2StartDate");
		AddString(ProfessionalExperiences.Emp2EndDate?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp2EndDate");
		AddString(ProfessionalExperiences.Emp2DatePermittedToContact?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp2DatePermittedToContact");
		AddString(ProfessionalExperiences.Emp2SupervisorName, "ProfessionalExperiences.Emp2SupervisorName");
		AddString(ProfessionalExperiences.Emp2SupervisorContactNumber, "ProfessionalExperiences.Emp2SupervisorContactNumber");
		AddFile(ProfessionalExperiences.Emp2COEUploadFile, "ProfessionalExperiences.Emp2COEUploadFile");
		AddString(ProfessionalExperiences.Emp2COEUploadFileName, "ProfessionalExperiences.Emp2COEUploadFileName");

		AddString(ProfessionalExperiences.Emp3CompanyName, "ProfessionalExperiences.Emp3CompanyName");
		AddString(ProfessionalExperiences.Emp3JobTitle, "ProfessionalExperiences.Emp3JobTitle");
		AddString(ProfessionalExperiences.Emp3CurrentlyEmployed?.ToString(), "ProfessionalExperiences.Emp3CurrentlyEmployed");
		AddString(ProfessionalExperiences.Emp3PermissionToContact?.ToString(), "ProfessionalExperiences.Emp3PermissionToContact");
		AddString(ProfessionalExperiences.Emp3CompanyCity, "ProfessionalExperiences.Emp3CompanyCity");
		AddString(ProfessionalExperiences.Emp3CompanyProvince, "ProfessionalExperiences.Emp3CompanyProvince");
		AddString(ProfessionalExperiences.Emp3CompanyCountry, "ProfessionalExperiences.Emp3CompanyCountry");
		AddString(ProfessionalExperiences.Emp3CompanyPostalCode, "ProfessionalExperiences.Emp3CompanyPostalCode");
		AddString(ProfessionalExperiences.Emp3StartDate?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp3StartDate");
		AddString(ProfessionalExperiences.Emp3EndDate?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp3EndDate");
		AddString(ProfessionalExperiences.Emp3DatePermittedToContact?.ToString("yyyy-MM-dd"), "ProfessionalExperiences.Emp3DatePermittedToContact");
		AddString(ProfessionalExperiences.Emp3SupervisorName, "ProfessionalExperiences.Emp3SupervisorName");
		AddString(ProfessionalExperiences.Emp3SupervisorContactNumber, "ProfessionalExperiences.Emp3SupervisorContactNumber");
		AddFile(ProfessionalExperiences.Emp3COEUploadFile, "ProfessionalExperiences.Emp3COEUploadFile");
		AddString(ProfessionalExperiences.Emp3COEUploadFileName, "ProfessionalExperiences.Emp3COEUploadFileName");

		// ReferenceDetails
		AddString(ReferenceDetails.ReferenceDetailsID.ToString(), "ReferenceDetails.ReferenceDetailsID");
		AddString(ReferenceDetails.EmailInvitationID.ToString(), "ReferenceDetails.EmailInvitationID");
		AddString(ReferenceDetails.Ref1FullName, "ReferenceDetails.Ref1FullName");
		AddString(ReferenceDetails.Ref1ProfessionalRelationship, "ReferenceDetails.Ref1ProfessionalRelationship");
		AddString(ReferenceDetails.Ref1AffiliatedCompany, "ReferenceDetails.Ref1AffiliatedCompany");
		AddString(ReferenceDetails.Ref1Email, "ReferenceDetails.Ref1Email");
		AddString(ReferenceDetails.Ref1ContactNumber, "ReferenceDetails.Ref1ContactNumber");
		AddString(ReferenceDetails.Ref1ModeOfContact, "ReferenceDetails.Ref1ModeOfContact");
		AddString(ReferenceDetails.Ref1BestTimeToContact?.ToString("o"), "ReferenceDetails.Ref1BestTimeToContact");
		AddString(ReferenceDetails.Ref2FullName, "ReferenceDetails.Ref2FullName");
		AddString(ReferenceDetails.Ref2ProfessionalRelationship, "ReferenceDetails.Ref2ProfessionalRelationship");
		AddString(ReferenceDetails.Ref2AffiliatedCompany, "ReferenceDetails.Ref2AffiliatedCompany");
		AddString(ReferenceDetails.Ref2Email, "ReferenceDetails.Ref2Email");
		AddString(ReferenceDetails.Ref2ContactNumber, "ReferenceDetails.Ref2ContactNumber");
		AddString(ReferenceDetails.Ref2ModeOfContact, "ReferenceDetails.Ref2ModeOfContact");
		AddString(ReferenceDetails.Ref2BestTimeToContact?.ToString("o"), "ReferenceDetails.Ref2BestTimeToContact");
		AddString(ReferenceDetails.Ref3FullName, "ReferenceDetails.Ref3FullName");
		AddString(ReferenceDetails.Ref3ProfessionalRelationship, "ReferenceDetails.Ref3ProfessionalRelationship");
		AddString(ReferenceDetails.Ref3AffiliatedCompany, "ReferenceDetails.Ref3AffiliatedCompany");
		AddString(ReferenceDetails.Ref3Email, "ReferenceDetails.Ref3Email");
		AddString(ReferenceDetails.Ref3ContactNumber, "ReferenceDetails.Ref3ContactNumber");
		AddString(ReferenceDetails.Ref3ModeOfContact, "ReferenceDetails.Ref3ModeOfContact");
		AddString(ReferenceDetails.Ref3BestTimeToContact?.ToString("o"), "ReferenceDetails.Ref3BestTimeToContact");

		// Post
		AddString(SignatureDetails.SignatureDetailsID.ToString(), "SignatureDetails.SignatureDetailsID");
		AddString(SignatureDetails.EmailInvitationID.ToString(), "SignatureDetails.EmailInvitationID");
		AddFile(SignatureDetails.Signature, "SignatureDetails.Signature");
		AddString(SignatureDetails.SignatureFileName, "SignatureDetails.SignatureFileName");
		AddString(SignatureDetails.SignerName, "SignatureDetails.SignerName");
		AddString(SignatureDetails.SignatureDate.ToString("MM-dd-yyyy"), "SignatureDetails.SignatureDate");

		var response = await _httpClient.PostAsync("ats/addapplicationformdata", content);

		var successContentInfo = await response.Content.ReadFromJsonAsync<bool>();
		return successContentInfo;
	}

	public async Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string HashToken)
	{
		var response = await _httpClient.GetFromJsonAsync<EmailIdAndApplicationFormPathDTO>($"getemailidandapplicationformpath?hashToken={HashToken}");
		if (response!.ExpiresAt < DateTime.UtcNow)
		{
			response!.IsExpired = true;
		}

		return response;
	}
}
