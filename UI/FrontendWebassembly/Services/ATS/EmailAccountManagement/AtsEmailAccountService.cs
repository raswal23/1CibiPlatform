namespace FrontendWebassembly.Services.ATS.EmailAccountManagement;

public class AtsEmailAccountService : IAtsEmailAccountService
{
	private readonly HttpClient _httpClient;

	public AtsEmailAccountService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	// The Carter endpoints wrap their payloads in a named record, so these mirror that shape
	// rather than posting the DTO bare.
	private sealed record GetEmailAccountsResponse(List<EmailAccountDTO> emailAccounts);
	private sealed record OtpSentResponse(EmailAccountOtpSentDTO otpSent);
	private sealed record EditResponse(EmailAccountOtpSentDTO? otpSent, bool requiresVerification);
	private sealed record VerifyResponse(EmailAccountOtpResultDTO result);

	public async Task<ServiceResponse<List<EmailAccountDTO>>> GetAccountsAsync(
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _httpClient.GetAsync("ats/getemailaccounts", cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<List<EmailAccountDTO>>.Failure(
					await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content
				.ReadFromJsonAsync<GetEmailAccountsResponse>(cancellationToken: cancellationToken);

			if (result?.emailAccounts is null)
			{
				return ServiceResponse<List<EmailAccountDTO>>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<List<EmailAccountDTO>>.Success(result.emailAccounts);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<List<EmailAccountDTO>>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<EmailAccountOtpSentDTO>> RegisterAsync(
		RegisterEmailAccountDTO account,
		CancellationToken cancellationToken = default)
	{
		var request = new { emailAccount = account };

		try
		{
			var response = await _httpClient.PostAsJsonAsync(
				"ats/registeremailaccount", request, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				// Carries the provider's own refusal - "authentication failed, check the app
				// password" - which is the whole reason the code is sent before the row is
				// trusted. Surfacing it verbatim is the point.
				return ServiceResponse<EmailAccountOtpSentDTO>.Failure(
					await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content
				.ReadFromJsonAsync<OtpSentResponse>(cancellationToken: cancellationToken);

			if (result?.otpSent is null)
			{
				return ServiceResponse<EmailAccountOtpSentDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<EmailAccountOtpSentDTO>.Success(result.otpSent);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<EmailAccountOtpSentDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<EmailAccountOtpSentDTO?>> EditAsync(
		EditEmailAccountDTO account,
		CancellationToken cancellationToken = default)
	{
		var request = new { editEmailAccount = account };

		try
		{
			var response = await _httpClient.PatchAsJsonAsync(
				"ats/editemailaccount", request, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<EmailAccountOtpSentDTO?>.Failure(
					await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content
				.ReadFromJsonAsync<EditResponse>(cancellationToken: cancellationToken);

			// A null otpSent is the success case for an edit that needed no verification, so
			// it is returned as a successful response carrying null rather than as a failure.
			return ServiceResponse<EmailAccountOtpSentDTO?>.Success(result?.otpSent);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<EmailAccountOtpSentDTO?>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<EmailAccountOtpSentDTO>> DeleteAsync(
		DeleteEmailAccountDTO request,
		CancellationToken cancellationToken = default)
	{
		var payload = new { deleteEmailAccount = request };

		try
		{
			// Post, not Delete: this sends a code. The row goes when the code comes back.
			var response = await _httpClient.PostAsJsonAsync(
				"ats/deleteemailaccount", payload, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<EmailAccountOtpSentDTO>.Failure(
					await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content
				.ReadFromJsonAsync<OtpSentResponse>(cancellationToken: cancellationToken);

			if (result?.otpSent is null)
			{
				return ServiceResponse<EmailAccountOtpSentDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<EmailAccountOtpSentDTO>.Success(result.otpSent);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<EmailAccountOtpSentDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<EmailAccountOtpResultDTO>> VerifyOtpAsync(
		VerifyEmailAccountOtpDTO request,
		CancellationToken cancellationToken = default)
	{
		var payload = new { verification = request };

		try
		{
			var response = await _httpClient.PostAsJsonAsync(
				"ats/verifyemailaccountotp", payload, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<EmailAccountOtpResultDTO>.Failure(
					await response.ReadErrorDetailAsync(cancellationToken));
			}

			// A wrong code is a 200 whose result says so, carrying the attempts left. Only a
			// malformed request or a server fault reaches the branch above.
			var result = await response.Content
				.ReadFromJsonAsync<VerifyResponse>(cancellationToken: cancellationToken);

			if (result?.result is null)
			{
				return ServiceResponse<EmailAccountOtpResultDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<EmailAccountOtpResultDTO>.Success(result.result);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<EmailAccountOtpResultDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}

	public async Task<ServiceResponse<EmailAccountOtpSentDTO>> ResendOtpAsync(
		ResendEmailAccountOtpDTO request,
		CancellationToken cancellationToken = default)
	{
		var payload = new { resend = request };

		try
		{
			var response = await _httpClient.PostAsJsonAsync(
				"ats/resendemailaccountotp", payload, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return ServiceResponse<EmailAccountOtpSentDTO>.Failure(
					await response.ReadErrorDetailAsync(cancellationToken));
			}

			var result = await response.Content
				.ReadFromJsonAsync<OtpSentResponse>(cancellationToken: cancellationToken);

			if (result?.otpSent is null)
			{
				return ServiceResponse<EmailAccountOtpSentDTO>.Failure("The server returned an empty response.");
			}

			return ServiceResponse<EmailAccountOtpSentDTO>.Success(result.otpSent);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return ServiceResponse<EmailAccountOtpSentDTO>.Failure($"Unable to reach the server. {ex.Message}");
		}
	}
}
