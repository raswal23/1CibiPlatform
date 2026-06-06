namespace ATS.Data.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
	private readonly ATSDBContext _atsDBContext;
	private IDbContextTransaction? _transaction;

	public UnitOfWork(ATSDBContext dbContext)
	{
		_atsDBContext = dbContext;
	}

	public async Task BeginTransactionAsync(CancellationToken ct = default)
	{
		_transaction = await _atsDBContext.Database.BeginTransactionAsync(ct);
	}

	public async Task CommitAsync(CancellationToken ct = default)
	{
		await _atsDBContext.SaveChangesAsync(ct);

		if (_transaction != null)
		{
			await _transaction.CommitAsync(ct);
		}
	}

	public async Task RollbackAsync(CancellationToken ct = default)
	{
		if (_transaction != null)
		{
			await _transaction.RollbackAsync(ct);
		}
	}
}