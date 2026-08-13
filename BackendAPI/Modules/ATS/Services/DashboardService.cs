namespace ATS.Services;

public class DashboardService : IDashboardService
{
	private readonly IATSRepository _atsRepository;
	private readonly AtsQueryScopeResolver _scopeResolver;

	public DashboardService(
		IATSRepository atsRepository,
		AtsQueryScopeResolver scopeResolver)
	{
		_atsRepository = atsRepository;
		_scopeResolver = scopeResolver;
	}

	public async Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		CancellationToken cancellationToken)
	{
		var scope = await _scopeResolver.ResolveAsync(cancellationToken);
		if (scope.Kind == AtsQueryScopeKind.Denied)
			return new ATSDashboardDTO();

		return await _atsRepository.GetDashboardAsync(
			requester,
			scope,
			cancellationToken);
	}
}
