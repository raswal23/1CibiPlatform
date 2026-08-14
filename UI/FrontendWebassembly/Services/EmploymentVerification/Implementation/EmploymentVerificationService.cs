using FrontendWebassembly.DTO.EmploymentVerification;
using FrontendWebassembly.Services.EmploymentVerification.Interface;

namespace FrontendWebassembly.Services.EmploymentVerification.Implementation;

public sealed class EmploymentVerificationService(
    IHttpClientFactory httpClientFactory,
    ILogger<EmploymentVerificationService> logger) : IEmploymentVerificationService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("API");

    // ProblemDetails is emitted in camelCase; without this the title/detail bind
    // to null and the raw JSON body leaks into the page.
    private static readonly System.Text.Json.JsonSerializerOptions ErrorSerializerOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public async Task<EmploymentVerificationResponseDTO<IReadOnlyList<EmploymentVerificationRequestDTO>>> GetRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
				"employmentverification/getatsinprogress",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Employment Verification request list failed. Reading error content...");

                var errorMessage = await GetErrorMessageAsync(
                    response,
                    cancellationToken);

                return new(null, "", errorMessage);
            }

            var requests = await response.Content.ReadFromJsonAsync<
                List<EmploymentVerificationRequestDTO>>(
                    cancellationToken: cancellationToken) ?? [];

            return new(requests, "", "");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Employment Verification request list could not be loaded.");

            return new(
                null,
                "",
                exception.Message);
        }
    }

    public async Task<EmploymentVerificationResponseDTO<IReadOnlyList<ATSInProgressEmploymentRecordDTO>>> GetInProgressATSRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "employmentverification/getatsinprogress",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new(
                    null,
                    "",
                    await GetErrorMessageAsync(response, cancellationToken));
            }

            var records = await response.Content.ReadFromJsonAsync<
                List<ATSInProgressEmploymentRecordDTO>>(
                    cancellationToken: cancellationToken) ?? [];

            return new(records, "", "");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "ATS employment records could not be loaded.");
            return new(null, "", exception.Message);
        }
    }

    public async Task<EmploymentVerificationResponseDTO<EmploymentVerificationResponseDetailsDTO>> CreateAndSendAsync(
        CreateEmploymentVerificationRequestDTO request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "employmentverification/createrequest",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Employment Verification email request failed for {Email}.",
                    request.HrEmail);

                return new(
                    null,
                    "",
                    await GetErrorMessageAsync(response, cancellationToken));
            }

            var created = await response.Content
                .ReadFromJsonAsync<EmploymentVerificationResponseDetailsDTO>(
                    cancellationToken: cancellationToken);

            return new(created, $"Verification email sent successfully to {created!.CandidateName}", "");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Employment Verification email request failed for {Email}.",
                request.HrEmail);

            return new(null, "", exception.Message);
        }
    }

    public Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>> VerifyAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        CompleteVerificationAsync(
            token,
            "verify",
            cancellationToken);

    public Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>> RejectAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        CompleteVerificationAsync(
            token,
            "reject",
            cancellationToken);

    public async Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>> GetPreviewAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new(
                null,
                "This verification link is missing its token.",
                VerificationLinkFailure.NotFound);
        }

        try
        {
            var response = await _httpClient.GetAsync(
                $"employmentverification/preview/{Uri.EscapeDataString(token)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Employment Verification preview returned {StatusCode}.",
                    (int)response.StatusCode);

                return await ReadFailureAsync<EmploymentVerificationPreviewDTO>(
                    response,
                    cancellationToken);
            }

            var request = await response.Content
                .ReadFromJsonAsync<EmploymentVerificationPreviewDTO>(
                    cancellationToken: cancellationToken);

            return request is null
                ? new(
                    null,
                    "The verification request could not be read.",
                    VerificationLinkFailure.Unknown)
                : new(request, "", VerificationLinkFailure.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Employment Verification preview failed.");

            return new(
                null,
                exception.Message,
                VerificationLinkFailure.Unknown);
        }
    }

    private async Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>>
        CompleteVerificationAsync(
            string token,
            string action,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new(
                null,
                "This verification link is missing its token.",
                VerificationLinkFailure.NotFound);
        }

        try
        {
            var response = await _httpClient.PostAsync(
                $"employmentverification/{action}/{Uri.EscapeDataString(token)}",
                content: null,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Employment Verification {Action} returned {StatusCode}.",
                    action,
                    (int)response.StatusCode);

                return await ReadFailureAsync<EmploymentVerificationPreviewDTO>(
                    response,
                    cancellationToken);
            }

            var result = await response.Content
                .ReadFromJsonAsync<EmploymentVerificationPreviewDTO>(
                    cancellationToken: cancellationToken);

            return result is null
                ? new(
                    null,
                    "The response could not be read.",
                    VerificationLinkFailure.Unknown)
                : new(result, "", VerificationLinkFailure.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Employment Verification response failed.");

            return new(
                null,
                exception.Message,
                VerificationLinkFailure.Unknown);
        }
    }

    /// <summary>
    /// Reads a problem response and maps its title onto the failure reason so the
    /// page can render a tailored state. The API detail is preserved for display.
    /// </summary>
    private async Task<VerificationLinkResultDTO<T>> ReadFailureAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiErrorResponse? error = null;

        try
        {
            error = System.Text.Json.JsonSerializer.Deserialize<ApiErrorResponse>(
                rawContent,
                ErrorSerializerOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            logger.LogError(
                "Employment Verification API returned a non-standard error body: {Body}",
                rawContent);
        }

        var failure = error?.Title switch
        {
            "TokenExpired" => VerificationLinkFailure.Expired,
            "TokenAlreadyUsed" => VerificationLinkFailure.AlreadyUsed,
            "TokenNotFound" => VerificationLinkFailure.NotFound,
            _ => VerificationLinkFailure.Unknown
        };

        var message = !string.IsNullOrWhiteSpace(error?.Detail)
            ? error.Detail
            : $"Request failed with status {(int)response.StatusCode}.";

        return new(default, message, failure);
    }

    private async Task<Exception> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        ApiErrorResponse? errorContent = null;

        try
        {
            errorContent = System.Text.Json.JsonSerializer.Deserialize<ApiErrorResponse>(rawContent);
        }
        catch (System.Text.Json.JsonException)
        {
            logger.LogError("Employment Verification API returned a non-standard error body: {Body}", rawContent);
        }

        logger.LogError(
            "Employment Verification API error detail: {Detail}",
            errorContent?.Detail);

        return new InvalidOperationException(
            errorContent?.Detail
            ?? (string.IsNullOrWhiteSpace(rawContent)
                ? $"Request failed with status {(int)response.StatusCode}."
                : rawContent));
    }

    private async Task<string> GetErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var error = System.Text.Json.JsonSerializer.Deserialize<ApiErrorResponse>(
                rawContent,
                ErrorSerializerOptions);

            return !string.IsNullOrWhiteSpace(error?.Detail)
                ? error.Detail
                : string.IsNullOrWhiteSpace(rawContent)
                    ? $"Request failed with status {(int)response.StatusCode}."
                    : rawContent;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.IsNullOrWhiteSpace(rawContent)
                ? $"Request failed with status {(int)response.StatusCode}."
                : rawContent;
        }
    }
}
