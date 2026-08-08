namespace FrontendWebassembly.Services.ATS.Interface;

public interface IClientManagementService
{
	Task<GetClientsResponseDTO> GetClientsAsync(int pageIndex, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<ClientDetailsDTO>> GetAllClientsAsync(CancellationToken cancellationToken = default);
	Task<bool> AddClientAsync(AddClientDTO clientDTO, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<ClientDetailsDTO>> EditClientAsync(EditClientDTO clientDTO, CancellationToken cancellationToken = default);
}
