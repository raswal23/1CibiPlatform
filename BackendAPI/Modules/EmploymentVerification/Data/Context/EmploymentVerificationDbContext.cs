namespace EmploymentVerification.Data.Context;

public sealed class EmploymentVerificationDbContext(DbContextOptions<EmploymentVerificationDbContext> options) : DbContext(options)
{
	public DbSet<EmploymentVerificationRequest> Requests => Set<EmploymentVerificationRequest>();
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmploymentVerificationDbContext).Assembly);
		base.OnModelCreating(modelBuilder);
	}
}
