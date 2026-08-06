namespace ATS.Services;

public interface IClientManagementService
{
	Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(
		PaginationRequest paginationRequest,
		CancellationToken cancellationToken);

	Task<bool> AddClientAsync(AddClientDTO clientDTO);

	Task<ClientDetailsDTO> EditClientAsync(EditClientDTO clientDTO);
}
