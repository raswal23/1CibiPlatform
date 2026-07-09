namespace PhilSys.Services;

public interface IUpdateFaceLivenessSessionService
{
	Task<VerificationResponseDTO> UpdateFaceLivenessSessionAsync(
		string HashToken,
		string FaceLivenessSessionId
		);
}
