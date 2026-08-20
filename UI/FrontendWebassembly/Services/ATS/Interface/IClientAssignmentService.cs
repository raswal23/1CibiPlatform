namespace FrontendWebassembly.Services.ATS.Interface;

public interface IClientAssignmentService
{
	Task<ServiceResponse<KeysetPaginatedResult<ClientAssignmentDetailsDTO>>> GetAssignmentsAsync(
		string? cursor,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<KeysetPaginatedResult<ClientLookupDTO>>> GetAssignableClientsAsync(
		string? cursor,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<ClientAssignmentDetailsDTO>> AssignClientAsync(
		AssignATSUserClientDTO assignment,
		CancellationToken cancellationToken = default);
}
