namespace ATS.Services;

public interface IBulkSubmissionProcessorService
{
	Task ProcessAsync(CancellationToken cancellationToken);
}
