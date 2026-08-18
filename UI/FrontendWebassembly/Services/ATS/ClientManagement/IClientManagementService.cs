namespace FrontendWebassembly.Services.ATS.ClientManagement;

public interface IClientManagementService
{
	Task<ServiceResponse<GetClientsResponseDTO>> GetClientsAsync(int pageIndex, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default);
	Task<ServiceResponse<IReadOnlyList<ClientDetailsDTO>>> GetAllClientsAsync(CancellationToken cancellationToken = default);
	Task<ServiceResponse<bool>> AddClientAsync(AddClientDTO clientDTO, CancellationToken cancellationToken = default);
	Task<ServiceResponse<IReadOnlyList<ClientDetailsDTO>>> EditClientAsync(EditClientDTO clientDTO, CancellationToken cancellationToken = default);
}
