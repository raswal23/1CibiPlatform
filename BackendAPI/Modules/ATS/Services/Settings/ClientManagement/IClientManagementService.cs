namespace ATS.Services.Settings.ClientManagement;

public interface IClientManagementService
{
	Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> AddClientAsync(
		IReadOnlyCollection<AddClientDTO> clientDTOs,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<ClientDetailsDTO>> EditClientAsync(
		IReadOnlyCollection<EditClientDTO> clientDTOs,
		CancellationToken cancellationToken);
}
