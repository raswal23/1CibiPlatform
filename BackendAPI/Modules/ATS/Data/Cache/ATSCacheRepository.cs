namespace ATS.Data.Cache;

public partial class ATSCacheRepository : IATSRepository
{
	private readonly IATSRepository _atsRepository;
	private readonly HybridCache _hybridCache;

	public ATSCacheRepository(IATSRepository atsRepository, HybridCache hybridCache)
	{
		_atsRepository = atsRepository;
		_hybridCache = hybridCache;
	}

	private static string ClientScope(IReadOnlyCollection<int>? authorizedClientIds) =>
		authorizedClientIds is null
			? "all"
			: string.Join('-', authorizedClientIds.OrderBy(clientId => clientId));

	private static string RequestorScope(Guid? requiredRequestorId) =>
		requiredRequestorId?.ToString("N") ?? "all";
}
