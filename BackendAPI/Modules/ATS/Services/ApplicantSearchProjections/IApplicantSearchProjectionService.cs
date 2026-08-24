namespace ATS.Services.ApplicantSearchProjections;

public interface IApplicantSearchProjectionService
{
	Task ProcessPendingProjectionsAsync(CancellationToken cancellationToken = default);
}
