namespace PhilSys.Services;

public class PhilSysService : IPhilSysService
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<PhilSysService> _logger;
	public PhilSysService(
	   IHttpClientFactory httpClientFactory,
	   ILogger<PhilSysService> logger)
	{
		_httpClient = httpClientFactory.CreateClient("PhilSys");
		_logger = logger;
	}


	// Get PhilSys Token
	public async Task<string> GetPhilsysTokenAsync(
		string clientId,
		string clientSecret)
	{
		var body = new
		{
			client_id = clientId,
			client_secret = clientSecret
		};

		var logContext = new
		{
			Action = "FetchingTokenfromPhilsys",
			Step = "RequestToken",
			clientId,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Requesting PhilSys token with {ClientId}: {@Context}", clientId, logContext);

		var response = await SendRequestAsync("auth", body);

		var responseBody = await response.Content.ReadFromJsonAsync<PhilSysTokenResponse>();

		if (!response.IsSuccessStatusCode)
		{
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError(
					"PhilSys auth failed. StatusCode: {StatusCode}, Response: {Response}",
					response.StatusCode,
					responseBody);

				throw new Exception(
					$"PhilSys token request failed. Status: {response.StatusCode}");
			}
		}

		_logger.LogInformation("Successful Request for Token: {@Context}", logContext);

		var tokenData = responseBody!.data;

		return tokenData.access_token;
	}

	// Post Basic Information
	public async Task<BasicInformationOrPCNResponseDTO> PostBasicInformationAsync(
		string first_name,
		string middle_name,
		string last_name,
		string suffix,
		string birth_date,
		string bearer_token,
		string face_liveness_session_id
		)
	{

		var body = new
		{
			first_name,
			middle_name,
			last_name,
			suffix,
			birth_date,
			face_liveness_session_id
		};

		var logContext = new
		{
			Action = "PostingBasicInformation",
			Step = "Post",
			Name=$"{first_name} {middle_name} {last_name} {suffix}",
			Timestamp = DateTime.UtcNow
		};

		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer_token);

		var response = await SendRequestAsync("query", body);

		_logger.LogInformation("Sending basic information request for {@Context}", logContext);

		if (!response.IsSuccessStatusCode)
		{
			var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDTO>();

			_logger.LogError("Basic Information request failed: {@Context}", logContext);

			throw new InternalServerException("Basic Information request failed. Please contact the administrator.");
		}

		_logger.LogInformation("Successful Basic Information Request: {@Context}", logContext);

		var responseBody = await response.Content.ReadFromJsonAsync<PostBasicInformationOrPCNResponse>();

		var returnData = responseBody!.data;

		return ReturnData(returnData);
	}

	// Post PhilSys Card Number
	public async Task<BasicInformationOrPCNResponseDTO> PostPCNAsync(
		string value,
		string bearer_token,
		string face_liveness_session_id
		)
	{

		var body = new
		{
			value,
			face_liveness_session_id
		};

		var logContext = new
		{
			Action = "PostingPCN",
			Step = "Post",
			PCN = value,
			Timestamp = DateTime.UtcNow
		};

		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer_token);

		var response = await SendRequestAsync("query/qr", body);

		_logger.LogInformation("Sending PCN request for: {@Context}", logContext);

		if (!response.IsSuccessStatusCode)
		{
			var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDTO>();

			_logger.LogError("PCN request failed: {@Context}", logContext);

			throw new InternalServerException("PCN request failed. Please contact the administrator.");
		}

		_logger.LogInformation("Successful PCN Request: {@Context}", logContext);

		var responseBody = await response.Content.ReadFromJsonAsync<PostBasicInformationOrPCNResponse>();

		var returnData = responseBody!.data;

		return ReturnData(returnData);
	}

	private static BasicInformationOrPCNResponseDTO ReturnData(BasicInformationOrPCNResponseDTO BasicInformationOrPCNResponseDTO)
	{
		return new BasicInformationOrPCNResponseDTO(
			BasicInformationOrPCNResponseDTO.code,
			BasicInformationOrPCNResponseDTO.token,
			BasicInformationOrPCNResponseDTO.reference,
			BasicInformationOrPCNResponseDTO.face_url,
			BasicInformationOrPCNResponseDTO.full_name,
			BasicInformationOrPCNResponseDTO.first_name,
			BasicInformationOrPCNResponseDTO.middle_name,
			BasicInformationOrPCNResponseDTO.last_name,
			BasicInformationOrPCNResponseDTO.suffix,
			BasicInformationOrPCNResponseDTO.gender,
			BasicInformationOrPCNResponseDTO.marital_status,
			BasicInformationOrPCNResponseDTO.blood_type,
			BasicInformationOrPCNResponseDTO.email,
			BasicInformationOrPCNResponseDTO.mobile_number,
			BasicInformationOrPCNResponseDTO.birth_date,
			BasicInformationOrPCNResponseDTO.full_address,
			BasicInformationOrPCNResponseDTO.address_line_1,
			BasicInformationOrPCNResponseDTO.address_line_2,
			BasicInformationOrPCNResponseDTO.barangay,
			BasicInformationOrPCNResponseDTO.municipality,
			BasicInformationOrPCNResponseDTO.province,
			BasicInformationOrPCNResponseDTO.country,
			BasicInformationOrPCNResponseDTO.postal_code,
			BasicInformationOrPCNResponseDTO.present_full_address,
			BasicInformationOrPCNResponseDTO.present_address_line_1,
			BasicInformationOrPCNResponseDTO.present_address_line_2,
			BasicInformationOrPCNResponseDTO.present_barangay,
			BasicInformationOrPCNResponseDTO.present_municipality,
			BasicInformationOrPCNResponseDTO.present_province,
			BasicInformationOrPCNResponseDTO.present_country,
			BasicInformationOrPCNResponseDTO.present_postal_code,
			BasicInformationOrPCNResponseDTO.residency_status,
			BasicInformationOrPCNResponseDTO.place_of_birth,
			BasicInformationOrPCNResponseDTO.pob_municipality,
			BasicInformationOrPCNResponseDTO.pob_province,
			BasicInformationOrPCNResponseDTO.pob_country
			);
	}

	private async Task<HttpResponseMessage> SendRequestAsync(
		string endpoint,
		object body)
	{
		return await _httpClient.PostAsJsonAsync(endpoint, body);
	}
}
