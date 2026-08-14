namespace EmploymentVerification.Data.Repository;

public interface IEmploymentVerificationRepository
{
	Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(CancellationToken cancellationToken);
	Task<EmploymentVerificationRequest?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
	Task AddAsync(EmploymentVerificationRequest request, CancellationToken cancellationToken);
	Task SaveChangesAsync(CancellationToken cancellationToken);
}
