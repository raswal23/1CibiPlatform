
namespace FrontendWebassembly.Services.ATS.Implementation;

public class ATSService : IATSService
{
	private readonly HttpClient _httpClient;

	public ATSService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<bool> AddApplicationFormDataAsync(PersonalDetailsDTO PersonalDetails, AddressDetailsDTO AddressDetails, EducationalBackgroundDTO EducationalBackground, LicensesDetailsDTO LicensesDetails, ProfessionalExperiencesDTO ProfessionalExperiences, ReferenceDetailsDTO ReferenceDetails)
	{
		var requestInfo = new { PersonalDetails, AddressDetails, EducationalBackground, LicensesDetails, ProfessionalExperiences, ReferenceDetails };
		var responseInfo = await _httpClient.PostAsJsonAsync("ats/addapplicationformdata", requestInfo);
		if (!responseInfo.IsSuccessStatusCode)
		{
			return false;
		}

		var successContentInfo = await responseInfo.Content.ReadFromJsonAsync<bool>();
		return true;
	}
}
