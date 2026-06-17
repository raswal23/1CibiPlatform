namespace ATS.Shared.Implementations;

internal class ATSQueries : IATSQueries
{
	private readonly IATSRepository _atsRepository;

	public ATSQueries(IATSRepository atsRepository)
	{
		_atsRepository = atsRepository;
	}

	public async Task<string?> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken)
	{
		return await _atsRepository.IsHashTokenValidAsync(hashToken, cancellationToken);
	}
}
