namespace ATS.Services.BulkEmailNotificationProcessor;

public interface IBulkEmailNotificationProcessorService
{
	Task ProcessForPendingStatusAsync(CancellationToken cancellationToken);
}
