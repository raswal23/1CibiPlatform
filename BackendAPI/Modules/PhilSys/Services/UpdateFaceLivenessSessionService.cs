namespace PhilSys.Services;

public class UpdateFaceLivenessSessionService
{
	private readonly HttpClient _httpClient;
	private readonly IPhilSysRepository _philSysRepository;
	private readonly IPhilSysResultRepository _philSysResultRepository;
	private readonly ILogger<UpdateFaceLivenessSessionService> _logger;
	private readonly IPhilSysService _philSysService;
	private readonly IConfiguration _configuration;
	private readonly string client_id;
	private readonly string client_secret;

	public UpdateFaceLivenessSessionService(
		IHttpClientFactory httpClientFactory,
		IPhilSysRepository philSysRepository,
		IPhilSysResultRepository philSysResultRepository,
		ILogger<UpdateFaceLivenessSessionService> logger,
		IPhilSysService philsysService,
		IConfiguration configuration)
	{
		_httpClient = httpClientFactory.CreateClient("IDVClient");
		_philSysRepository = philSysRepository;
		_philSysResultRepository = philSysResultRepository;
		_logger = logger;
		_philSysService = philsysService;
		_configuration = configuration;
		client_id = _configuration["PhilSys:ClientID"] ?? "";
		client_secret = _configuration["PhilSys:ClientSecret"] ?? "";
	}

	public async Task<VerificationResponseDTO> UpdateFaceLivenessSessionAsync(
		string HashToken,
		string FaceLivenessSessionId,
		byte[] Photo
		)
	{
		string accessToken = string.Empty;
		BasicInformationOrPCNResponseDTO responseBody = null!;

		var logContext = new
		{
			Action = "GetVerificationResult",
			Step = "UpdateFaceLivenessSession",
			HashToken,
			FaceLivenessSessionId,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Updating Face Liveness Session for Token: {@Context}", logContext);

		var result = await _philSysRepository.UpdateFaceLivenessSessionAsync(HashToken, FaceLivenessSessionId, Photo);
		if (result == null)
		{
			_logger.LogError("No transaction found for {HashToken}: {@Context}", HashToken, logContext);
			throw new InternalServerException("No transaction record found for your Token. Face Liveness Session update aborted.");
		}

		_logger.LogInformation("Successfully updated Face Liveness Session for Token: {@Context}", logContext);

		accessToken = await _philSysService.GetPhilsysTokenAsync(client_id, client_secret);
	

		if (result!.InquiryType!.Equals("name_dob", StringComparison.CurrentCultureIgnoreCase))
		{
			responseBody = await _philSysService.PostBasicInformationAsync(result.FirstName!, result.MiddleName!, result.LastName!, result.Suffix!, result.BirthDate!, accessToken, FaceLivenessSessionId);
		
			var convertedResponse = ConvertVerificationResponseDTO(result.Tid, responseBody!);

			await SendToClientWebHookAsync(result.WebHookUrl!, convertedResponse);

			await UpdateTransactionStatus(HashToken);

			await AddConvertedResponseToDbAsync(convertedResponse);

			return convertedResponse!;
		}

		if (result.InquiryType.Equals("pcn", StringComparison.OrdinalIgnoreCase))
		{
			responseBody = await _philSysService.PostPCNAsync(result.PCN!, accessToken, result.FaceLivenessSessionId!);
		
			var convertedResponse = ConvertVerificationResponseDTO(result.Tid, responseBody!);

			await SendToClientWebHookAsync(result.WebHookUrl!, convertedResponse);

			await UpdateTransactionStatus(HashToken);

			await AddConvertedResponseToDbAsync(convertedResponse);

			return convertedResponse!;
		}

		return new VerificationResponseDTO { };
	}

	private async Task<bool> UpdateTransactionStatus(string HashToken)
	{
		var logContext = new
		{
			Action = "UpdateTransactionStatus",
			Step = "UpdateTransaction",
			HashToken,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Updating the Transaction Status: {@Context}", logContext);

		var existingTransaction = await _philSysRepository.GetTransactionDataByHashTokenAsync(HashToken);

		if (existingTransaction == null)
		{
			_logger.LogError("Update Status Failed: Transaction with {HashToken} not found: {@Context}", HashToken, logContext);
			throw new InternalServerException("No Transaction record found for this transaction. Please contact the administrator.");
		}

		var updateStatus = await _philSysRepository.UpdateTransactionDataAsync(existingTransaction);

		if (updateStatus == null)
		{
			_logger.LogError("Update Status Failed: Failed to Update the Transaction Status for {HashToken}: {@Context}", HashToken, logContext);
			return false;
		}

		_logger.LogInformation("Successfully Updated the Transaction Status: {@Context}", logContext);
		return true;
	}

	private async Task SendToClientWebHookAsync (string WebHook, VerificationResponseDTO VerificationResponseDTO)
	{
		var logContext = new
		{
			Action = "SendToClientWebhook",
			Step = "SendToWebhook",
			VerificationResponseDTO.idv_session_id,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Sending the verification response to client webhook: {@Context}", logContext);

		if (WebHook != "/")
		{
			var clientResponse = await _httpClient.PostAsJsonAsync(WebHook, VerificationResponseDTO);
			if (!clientResponse.IsSuccessStatusCode)
			{
				_logger.LogError("Failed To Send to Webhook: Failed to send verification response to {WebHook}: {@Context}", WebHook, logContext);
				throw new InternalServerException("Failed to send verification response to client's webhook. Please contact the administrator.");
			}
			_logger.LogInformation("Successfully send the verification response to client webhook: {@Context}", logContext);
		}
	}

	private async Task<bool> AddConvertedResponseToDbAsync(VerificationResponseDTO VerificationResponseDTO)
	{
		var logContext = new
		{
			Action = "AddingTheVerificationResult",
			Step = "AddResult",
			VerificationResponseDTO.idv_session_id,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding the Converted Response in PhilSys Transaction Results' Table: {@Context}", logContext);

		var philsysTransactionResult = VerificationResponseDTO.Adapt<PhilSysTransactionResult>();
		var result = await _philSysResultRepository.AddTransactionResultDataAsync(philsysTransactionResult);
		if (result == false)
		{
			_logger.LogError("Saved Transaction Failed: Failed to Add the Converted Response in PhilSys Transaction Results' Table: {@Context}", logContext);
			throw new InternalServerException("Failed to Add the Converted Response in PhilSys Transaction Results.");
		}
		_logger.LogInformation("Successfully Added the Converted Response in PhilSys Transaction Results' Table: {@Context}", logContext);
		return true;
	}

	private static VerificationResponseDTO ConvertVerificationResponseDTO(Guid Tid, BasicInformationOrPCNResponseDTO BasicInformationOrPCNResponseDTO)
	{
		if (string.IsNullOrEmpty(BasicInformationOrPCNResponseDTO.reference) )
		{
			return new VerificationResponseDTO
			{
				idv_session_id = Tid.ToString(),
				verified = false
			}; 
		}
		return new VerificationResponseDTO
		{
			idv_session_id = Tid.ToString(),
			verified = true,
			data_subject = new DataSubject
			{
				digital_id = BasicInformationOrPCNResponseDTO.code,
				national_id_number = BasicInformationOrPCNResponseDTO.reference,
				face_url = BasicInformationOrPCNResponseDTO.face_url,
				full_name = BasicInformationOrPCNResponseDTO.full_name,
				first_name = BasicInformationOrPCNResponseDTO.first_name,
				middle_name = BasicInformationOrPCNResponseDTO.middle_name,
				last_name = BasicInformationOrPCNResponseDTO.last_name,
				suffix = BasicInformationOrPCNResponseDTO.suffix,
				gender = BasicInformationOrPCNResponseDTO.gender,
				marital_status = BasicInformationOrPCNResponseDTO.marital_status,
				blood_type = BasicInformationOrPCNResponseDTO.blood_type,
				email = BasicInformationOrPCNResponseDTO.email,
				mobile_number = BasicInformationOrPCNResponseDTO.mobile_number,
				birth_date = BasicInformationOrPCNResponseDTO.birth_date,
				full_address = BasicInformationOrPCNResponseDTO.full_address,
				address_line_1 = BasicInformationOrPCNResponseDTO.address_line_1,
				address_line_2 = BasicInformationOrPCNResponseDTO.address_line_2,
				barangay = BasicInformationOrPCNResponseDTO.barangay,
				municipality = BasicInformationOrPCNResponseDTO.municipality,
				province = BasicInformationOrPCNResponseDTO.province,
				country = BasicInformationOrPCNResponseDTO.country,
				postal_code = BasicInformationOrPCNResponseDTO.postal_code,	
				present_full_address = BasicInformationOrPCNResponseDTO.present_full_address,
				present_address_line_1 = BasicInformationOrPCNResponseDTO.present_address_line_1,
				present_address_line_2 = BasicInformationOrPCNResponseDTO.present_address_line_2,
				present_barangay = BasicInformationOrPCNResponseDTO.present_barangay,
				present_municipality = BasicInformationOrPCNResponseDTO.present_municipality,
				present_province = BasicInformationOrPCNResponseDTO.present_province,
				present_country = BasicInformationOrPCNResponseDTO.present_country,
				present_postal_code = BasicInformationOrPCNResponseDTO.present_postal_code,
				residency_status = BasicInformationOrPCNResponseDTO.residency_status,
				place_of_birth = BasicInformationOrPCNResponseDTO.place_of_birth,
				pob_municipality = BasicInformationOrPCNResponseDTO.pob_municipality,
				pob_province = BasicInformationOrPCNResponseDTO.pob_province,
				pob_country = BasicInformationOrPCNResponseDTO.country

			}
		};
	}
}

		