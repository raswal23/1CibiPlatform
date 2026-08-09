namespace FrontendWebassembly.Services.ATS.Interface;

public interface IDashboardService
{
	Task<ATSDashboardDTO> GetDashboardAsync(string? requester = null);
}
