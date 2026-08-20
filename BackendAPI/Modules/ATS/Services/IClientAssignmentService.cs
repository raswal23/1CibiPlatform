namespace ATS.Services;

public interface IClientAssignmentService
{
	Task<PaginatedResult<ClientAssignmentDetailsDTO>> GetAssignmentsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<PaginatedResult<ClientLookupDTO>> GetAssignableClientsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<ClientAssignmentDetailsDTO> AssignClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken);
}
