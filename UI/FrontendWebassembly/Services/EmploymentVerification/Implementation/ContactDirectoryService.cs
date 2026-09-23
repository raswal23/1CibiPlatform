namespace FrontendWebassembly.Services.EmploymentVerification.Implementation;

public sealed class ContactDirectoryService(IHttpClientFactory httpClientFactory)
	: IContactDirectoryService
{
	private readonly HttpClient _httpClient = httpClientFactory.CreateClient("API");

	// Public gateway paths, never the internal Carter route the PathSet transform
	// targets. See Path/EmploymentVerificationPaths.cs.
	private const string GetContactsPath = "employmentverification/getcontacts";
	private const string AddContactPath = "employmentverification/addcontact";
	private const string EditContactPath = "employmentverification/editcontact";

	public Task<ServiceResponse<KeysetPaginatedResult<EmploymentVerificationContactDTO>>> GetContactsAsync(
		string? cursor,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default)
	{
		var query = $"{GetContactsPath}?pageSize={pageSize}";

		if (!string.IsNullOrEmpty(cursor))
		{
			query += $"&cursor={Uri.EscapeDataString(cursor)}";
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
		}

		// The endpoint wraps the page in a Contacts property; projecting it here keeps
		// the envelope out of the pages.
		return ApiRequestExtensions.SendAsync<
			GetContactsResponseDTO,
			KeysetPaginatedResult<EmploymentVerificationContactDTO>>(
			() => _httpClient.GetAsync(query, cancellationToken),
			response => response.Contacts,
			cancellationToken);
	}

	public Task<ServiceResponse<bool>> AddContactAsync(
		AddEmploymentVerificationContactDTO contact,
		CancellationToken cancellationToken = default) =>
		ApiRequestExtensions.SendAsync<AddContactResponseDTO, bool>(
			() => _httpClient.PostAsJsonAsync(
				AddContactPath,
				new { Contact = contact },
				cancellationToken),
			// SendAsync's "null projection means empty response" guard cannot fire for a
			// bool, so a server reporting false still arrives as a successful response
			// carrying false rather than as a transport failure. That is the right split:
			// a refused write is a result, and the 409 path is already a failure status.
			response => response.IsAdded,
			cancellationToken);

	public Task<ServiceResponse<bool>> EditContactAsync(
		EditEmploymentVerificationContactDTO contact,
		CancellationToken cancellationToken = default) =>
		ApiRequestExtensions.SendAsync<EditContactResponseDTO, bool>(
			() => _httpClient.PatchAsJsonAsync(
				EditContactPath,
				new { Contact = contact },
				cancellationToken),
			response => response.IsUpdated,
			cancellationToken);

	private sealed class AddContactResponseDTO
	{
		public bool IsAdded { get; set; }
	}

	private sealed class EditContactResponseDTO
	{
		public bool IsUpdated { get; set; }
	}
}
