namespace ATS.Services.EmailNotificationRecovery;

public interface IEmailNotificationRecoveryService
{
	Task RequeueStaleBatchesAsync(CancellationToken cancellationToken);
}
