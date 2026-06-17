namespace FrontendWebassembly.Services.PhilSys.Interfaces;

public interface IPhilSysService
{
	Task<UpdateFaceLivenessSessionResponseDTO> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSession);

	Task<TransactionStatusResponseDTO> GetTransactionStatusAsync(string HashToken);
	Task<string> GetLivenessKeyAsync();

	Task<bool> DeleteTransactionAsync(string HashToken);

	Task<string> PostBasicInformationOrPCNAsync(string inquiry_type, IdentityData identity_data);
}
