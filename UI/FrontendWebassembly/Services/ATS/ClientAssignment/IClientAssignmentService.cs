namespace FrontendWebassembly.Services.ATS.ClientAssignment;

public interface IClientAssignmentService
{
	Task<ServiceResponse<GetClientAssignmentsResponseDTO>> GetAssignmentsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<GetClientLookupResponseDTO>> GetAssignableClientsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ServiceResponse<ClientAssignmentDetailsDTO>> AssignClientAsync(
		AssignATSUserClientDTO assignment,
		CancellationToken cancellationToken = default);
}
