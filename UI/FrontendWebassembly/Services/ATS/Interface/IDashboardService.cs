namespace FrontendWebassembly.Services.ATS.Interface;

public interface IDashboardService
{
	Task<ServiceResponse<ATSDashboardDTO>> GetDashboardAsync(string? requester = null);
}
