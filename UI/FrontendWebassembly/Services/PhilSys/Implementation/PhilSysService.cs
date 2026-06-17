namespace FrontendWebassembly.Services.PhilSys.Implementation;

public class PhilSysService : IPhilSysService
{
	private readonly HttpClient _httpClient;

	public PhilSysService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<UpdateFaceLivenessSessionResponseDTO> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSession)
	{
		var payload = new
		{
			HashToken,
			FaceLivenessSessionId = FaceLivenessSession
		};

		var response = await _httpClient.PatchAsJsonAsync("philsys/idv/updatefacelivenesssession", payload);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

			return new UpdateFaceLivenessSessionResponseDTO
			{
				idv_session_id = string.Empty,
				verified = null,
				data_subject = null,
				error_message = errorContent!.Detail,
				trace_id = errorContent.TraceId,
			};
		}
		var successContent = await response.Content.ReadFromJsonAsync<UpdateFaceLivenessSessionResponseDTO>();
		return successContent!;
	}

	public async Task<TransactionStatusResponseDTO> GetTransactionStatusAsync(string HashToken)
	{
		var request = new { HashToken };
		var response = await _httpClient.PostAsJsonAsync("/philsys/idv/validate/liveness", request);

		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
		
			return new TransactionStatusResponseDTO 
			{
				error_message = errorContent!.Detail,
				trace_id = errorContent.TraceId
			};
		}
		
		var successContent = await response.Content.ReadFromJsonAsync<TransactionStatusResponseDTO>();

		if (successContent!.ExpiresAt < DateTime.UtcNow)
		{
			successContent!.isExpired = true;
		}
		
		return successContent!;

	}

	public async Task<string> GetLivenessKeyAsync()
	{
		var response = await _httpClient.GetFromJsonAsync<string>("philsys/idv/getlivenesskey");
		if (string.IsNullOrEmpty(response!))
		{
			return string.Empty;
		}
		return response;
	}

	public async Task<bool> DeleteTransactionAsync(string HashToken)
	{
		var response = await _httpClient.DeleteAsync($"philsys/deletetransaction/{HashToken}");
		if (!response.IsSuccessStatusCode)
		{
			return false!;
		}

		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		return successContent!;
	}

	public async Task<string> PostBasicInformationOrPCNAsync(string inquiry_type, IdentityData identity_data)
	{
		if (DateTime.TryParse(identity_data.birth_date, out var parsedDate))
		{
			identity_data.birth_date = parsedDate.ToString("yyyy-MM-dd");
		}

		var endpoint = string.IsNullOrEmpty(identity_data.ats_session)
			? "philsys/idv"
			: "philsys/internal";

		if (inquiry_type == "name_dob")
		{
			var requestInfo = new { callback_url = "/", inquiry_type = "name_dob", identity_data };
			var responseInfo = await _httpClient.PostAsJsonAsync(endpoint, requestInfo);
			if (!responseInfo.IsSuccessStatusCode)
			{
				return "";
			}

			var successContentInfo = await responseInfo.Content.ReadFromJsonAsync<PostBasicInformationOrPCNResponseDTO>();
			return successContentInfo!.liveness_link!;
		}

		var requestPcn = new { callback_url = "/", inquiry_type = "pcn", identity_data };
		var responsePCn = await _httpClient.PostAsJsonAsync(endpoint, requestPcn);
		if (!responsePCn.IsSuccessStatusCode)
		{
			return "";
		}

		var successContentPcn = await responsePCn.Content.ReadFromJsonAsync<PostBasicInformationOrPCNResponseDTO>();
		return successContentPcn!.liveness_link!;

	}
}
