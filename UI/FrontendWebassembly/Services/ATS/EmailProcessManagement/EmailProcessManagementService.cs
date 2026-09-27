namespace FrontendWebassembly.Services.ATS.EmailProcessManagement;

public class EmailProcessManagementService : IEmailProcessManagementService
{
	private readonly HttpClient _httpClient;

	public EmailProcessManagementService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	// Only the list endpoint wraps its payload in a named record. Both writes return the DTO
	// itself - AddEmailProcessEndpoint and EditEmailProcessEndpoint each unwrap their response
	// record before Results.Ok - so there is no envelope to project out of on those two.
	private sealed record GetEmailProcessesResponse(List<EmailProcessDetailsDTO> emailProcesses);

	public Task<ServiceResponse<List<EmailProcessDetailsDTO>>> GetEmailProcessesAsync(
		CancellationToken cancellationToken = default) =>
		ApiRequestExtensions.SendAsync<GetEmailProcessesResponse, List<EmailProcessDetailsDTO>>(
			() => _httpClient.GetAsync("ats/getemailprocesses", cancellationToken),
			response => response.emailProcesses,
			cancellationToken);

	public Task<ServiceResponse<EmailProcessDetailsDTO>> AddEmailProcessAsync(
		AddEmailProcessDTO emailProcess,
		CancellationToken cancellationToken = default) =>
		ApiRequestExtensions.SendAsync<EmailProcessDetailsDTO>(
			() => _httpClient.PostAsJsonAsync(
				"ats/addemailprocess",
				new { emailProcess },
				cancellationToken),
			cancellationToken);

	// The wrapper name differs from the add's - editEmailProcess, not emailProcess - because that
	// is what EditEmailProcessRequest calls its parameter. Nothing enforces the agreement between
	// the two at compile time, so a rename on either side fails as a 400 with an empty body.
	public Task<ServiceResponse<EmailProcessDetailsDTO>> EditEmailProcessAsync(
		EditEmailProcessDTO emailProcess,
		CancellationToken cancellationToken = default) =>
		ApiRequestExtensions.SendAsync<EmailProcessDetailsDTO>(
			() => _httpClient.PatchAsJsonAsync(
				"ats/editemailprocess",
				new { editEmailProcess = emailProcess },
				cancellationToken),
			cancellationToken);
}
