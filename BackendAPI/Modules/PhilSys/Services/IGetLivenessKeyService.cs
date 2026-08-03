namespace PhilSys.Services;

public interface IGetLivenessKeyService
{
	Task<string> GetLivenessKey();
}
