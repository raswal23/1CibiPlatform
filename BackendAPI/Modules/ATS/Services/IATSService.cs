namespace ATS.Services;

public interface IATSService
{
	Task<bool> AddApplicationFormDataAsync(ApplicationFormDataDTO applicationFormDataDTO, CancellationToken ct = default);
}
