namespace ATS.Services;

public interface IDashboardService
{
	Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		CancellationToken cancellationToken);
}
