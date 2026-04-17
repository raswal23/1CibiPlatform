namespace FrontendWebassembly.Services.PhilSys.Interfaces;

public interface IPhilSysService
{
	Task<UpdateFaceLivenessSessionResponseDTO> UpdateFaceLivenessSessionAsync(string HashToken, string FaceLivenessSession, byte[] photo);

	Task<TransactionStatusResponseDTO> GetTransactionStatusAsync(string HashToken);
	Task<string> GetLivenessKeyAsync();

	Task<bool> DeleteTransactionAsync(string HashToken);

	Task<string> PostBasicInformationOrPCN(string inquiry_type, IdentityData identity_data);

}
