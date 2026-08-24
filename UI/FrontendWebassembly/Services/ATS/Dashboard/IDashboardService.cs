namespace FrontendWebassembly.Services.ATS.Dashboard;

public interface IDashboardService
{
	Task<ServiceResponse<ATSDashboardDTO>> GetDashboardAsync(string? requester = null);
}
