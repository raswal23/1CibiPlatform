namespace ATS.Services;

public class DashboardService : IDashboardService
{
	private readonly IATSRepository _atsRepository;

	public DashboardService(IATSRepository atsRepository)
	{
		_atsRepository = atsRepository;
	}

	public Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		CancellationToken cancellationToken)
	{
		return _atsRepository.GetDashboardAsync(requester, cancellationToken);
	}
}
