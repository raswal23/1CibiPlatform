namespace ATS.Services;

public interface IEmailNotificationProcessorService
{
	Task ProcessForPendingStatusAsync(CancellationToken cancellationToken);
	Task ProcessForErrorStatusAsync(CancellationToken cancellationToken);

}
