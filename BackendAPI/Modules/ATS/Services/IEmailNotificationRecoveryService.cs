namespace ATS.Services;

public interface IEmailNotificationRecoveryService
{
	Task RequeueStaleBatchesAsync(CancellationToken cancellationToken);
}
