namespace ATS.Services.BulkSubmissionProcessor;

public interface IBulkSubmissionProcessorService
{
	Task ProcessAsync(CancellationToken cancellationToken);
}
