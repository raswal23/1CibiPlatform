namespace PhilSys.Data.Context;

public class PhilSysDBContext : DbContext
{
	public PhilSysDBContext(DbContextOptions<PhilSysDBContext> options) : base(options)
	{
	}

	public DbSet<PhilSysTransaction> PhilSysTransactions { get; set; }
	public DbSet<PhilSysTransactionResult> PhilSysTransactionResults { get; set; }
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhilSysDBContext).Assembly);

		base.OnModelCreating(modelBuilder);
	}
}
