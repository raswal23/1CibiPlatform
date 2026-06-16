namespace ATS.Shared.Implementations;

internal class ATSQueries : IATSQueries
{
	private readonly ATSRepository _atsRepository;

	public ATSQueries(ATSRepository atsRepository)
	{
		_atsRepository = atsRepository;
	}

	public async Task<string?> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken)
	{
		return await _atsRepository.IsHashTokenValidAsync(hashToken, cancellationToken);
	}
}
