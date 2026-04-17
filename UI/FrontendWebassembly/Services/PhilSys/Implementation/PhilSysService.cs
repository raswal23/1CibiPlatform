namespace FrontendWebassembly.Services.PhilSys.Implementation;

public class PhilSysService : IPhilSysService
{
	private readonly HttpClient _httpClient;

	public PhilSysService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<UpdateFaceLivenessSessionResponseDTO> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSession, byte[] photo)
	{
		var payload = new
		{
			HashToken,
			FaceLivenessSessionId = FaceLivenessSession,
			photo
		};

		var response = await _httpClient.PatchAsJsonAsync("philsys/idv/updatefacelivenesssession", payload);

		if (!response.IsSuccessStatusCode)
		{
			Console.WriteLine("❌ Did not Update Successfully");
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

		Console.WriteLine("✅ Updated Successfully");

		return successContent!;
	}

	public async Task<TransactionStatusResponseDTO> GetTransactionStatusAsync(string HashToken)
	{
		var request = new { HashToken };
		var response = await _httpClient.PostAsJsonAsync("/philsys/idv/validate/liveness", request);

		if (!response.IsSuccessStatusCode)
		{
			Console.WriteLine("❌ Did not Get the Status");
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
		
		Console.WriteLine("✅ Retrieve the Status Successfully");
		 
		return successContent!;

	}

	public async Task<string> GetLivenessKeyAsync()
	{
		var response = await _httpClient.GetFromJsonAsync<string>("philsys/idv/getlivenesskey");
		if (string.IsNullOrEmpty(response!))
		{
			Console.WriteLine("❌ Did not Get Liveness Key");
			return string.Empty;
		}
		Console.WriteLine("✅ Retrieve Liveness Key Successfully");
		return response;
	}

	public async Task<bool> DeleteTransactionAsync(string HashToken)
	{
		var response = await _httpClient.DeleteAsync($"philsys/deletetransaction/{HashToken}");
		if (!response.IsSuccessStatusCode)
		{
			Console.WriteLine("❌ Did not Delete Successfully");
			return false!;
		}

		var successContent = await response.Content.ReadFromJsonAsync<bool>();
		Console.WriteLine("✅ Delete Successfully");
		return successContent!;
	}

	public async Task<string> PostBasicInformationOrPCN(string inquiry_type, IdentityData identity_data)
	{
		if (DateTime.TryParse(identity_data.birth_date, out var parsedDate))
		{
			identity_data.birth_date = parsedDate.ToString("yyyy-MM-dd");
		}
		if (inquiry_type == "name_dob")
		{
			var requestInfo = new { callback_url = "/", inquiry_type = "name_dob", identity_data };
			var responseInfo = await _httpClient.PostAsJsonAsync("philsys/idv", requestInfo);
			if (!responseInfo.IsSuccessStatusCode)
			{
				Console.WriteLine("❌ Did not Verified Successfully");
				return "";
			}

			var successContentInfo = await responseInfo.Content.ReadFromJsonAsync<PostBasicInformationOrPCNResponseDTO>();
			return successContentInfo!.liveness_link!;
		}

		var requestPcn = new { callback_url = "/", inquiry_type = "pcn", identity_data };
		var responsePCn = await _httpClient.PostAsJsonAsync("philsys/idv", requestPcn);
		if (!responsePCn.IsSuccessStatusCode)
		{
			Console.WriteLine("❌ Did not Verified Successfully");
			return "";
		}

		var successContentPcn = await responsePCn.Content.ReadFromJsonAsync<PostBasicInformationOrPCNResponseDTO>();
		return successContentPcn!.liveness_link!;

	}
}
