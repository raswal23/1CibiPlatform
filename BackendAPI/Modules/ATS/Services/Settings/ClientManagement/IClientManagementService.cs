namespace ATS.Services.Settings.ClientManagement;

public interface IClientManagementService
{
	Task<KeysetPaginatedResult<ClientDetailsDTO>> GetClientsAsync(
		KeysetPaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> AddClientAsync(
		IReadOnlyCollection<AddClientDTO> clientDTOs,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<ClientDetailsDTO>> EditClientAsync(
		IReadOnlyCollection<EditClientDTO> clientDTOs,
		CancellationToken cancellationToken);
}
