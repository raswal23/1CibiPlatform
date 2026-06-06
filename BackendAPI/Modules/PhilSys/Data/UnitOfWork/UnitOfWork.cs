using Microsoft.EntityFrameworkCore.Storage;

namespace PhilSys.Data.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
	private readonly PhilSysDBContext _philsysDBContext;
	private IDbContextTransaction? _transaction;

	public UnitOfWork(PhilSysDBContext philsysDBContext)
	{
		_philsysDBContext = philsysDBContext;
	}

	public async Task BeginTransactionAsync(CancellationToken ct = default)
	{
		_transaction = await _philsysDBContext.Database.BeginTransactionAsync(ct);
	}

	public async Task CommitAsync(CancellationToken ct = default)
	{
		await _philsysDBContext.SaveChangesAsync(ct);

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
