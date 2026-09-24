namespace EmploymentVerification.Data.Context;

public sealed class EmploymentVerificationDbContext(DbContextOptions<EmploymentVerificationDbContext> options) : DbContext(options)
{
	public DbSet<EmploymentVerificationRequest> Requests => Set<EmploymentVerificationRequest>();
	public DbSet<EmploymentVerificationContact> Contacts => Set<EmploymentVerificationContact>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmploymentVerificationDbContext).Assembly);
		base.OnModelCreating(modelBuilder);
	}
}
