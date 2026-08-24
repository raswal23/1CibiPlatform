namespace ATS.Services.EmailNotificationProcessor;

public interface IEmailNotificationProcessorService
{
	Task ProcessForPendingStatusAsync(CancellationToken cancellationToken);
}
