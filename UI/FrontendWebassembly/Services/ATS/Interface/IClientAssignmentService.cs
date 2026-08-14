namespace FrontendWebassembly.Services.ATS.Interface;

public interface IClientAssignmentService
{
	Task<GetClientAssignmentsResponseDTO> GetAssignmentsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<GetClientLookupResponseDTO> GetAssignableClientsAsync(
		int pageIndex,
		int pageSize,
		string? searchTerm = null,
		CancellationToken cancellationToken = default);

	Task<ClientAssignmentDetailsDTO> AssignClientAsync(
		AssignATSUserClientDTO assignment,
		CancellationToken cancellationToken = default);
}
