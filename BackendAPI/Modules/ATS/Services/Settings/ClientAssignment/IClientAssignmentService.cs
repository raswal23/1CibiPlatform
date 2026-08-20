namespace ATS.Services.Settings.ClientAssignment;

public interface IClientAssignmentService
{
	Task<KeysetPaginatedResult<ClientAssignmentDetailsDTO>> GetAssignmentsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<KeysetPaginatedResult<ClientLookupDTO>> GetAssignableClientsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<ClientAssignmentDetailsDTO> AssignClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken);
}
