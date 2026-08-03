namespace ATS.Services;

public interface IApplicantSearchProjectionService
{
	Task ProcessPendingProjectionsAsync(CancellationToken cancellationToken = default);
}
