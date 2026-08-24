namespace ATS.Services.Dashboard;

public interface IDashboardService
{
	Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		CancellationToken cancellationToken);
}
