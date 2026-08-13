namespace ATS.Data.Context;

public class ATSDBContext : DbContext
{
    public ATSDBContext(DbContextOptions<ATSDBContext> options) : base(options)
    {
    }

    public DbSet<EmailInvitationRequest> EmailInvitationRequests { get; set; }
    public DbSet<PersonalDetails> PersonalDetails { get; set; }
    public DbSet<AddressDetails> AddressDetails { get; set; }
    public DbSet<EducationalBackground> EducationalBackgrounds { get; set; }
    public DbSet<LicensesDetails> LicensesDetails { get; set; }
    public DbSet<ProfessionalExperiences> ProfessionalExperiences { get; set; }
    public DbSet<ReferenceDetails> ReferenceDetails { get; set; }
    public DbSet<DocumentDetails> DocumentDetails { get; set; }
	public DbSet<SignatureDetails> SignatureDetails { get; set; }
	public DbSet<BulkUploadFileDetails> BulkUploadFileDetails { get; set; }
    public DbSet<ReportDetails> ReportDetails { get; set; }
    public DbSet<ArchiveReport> ArchiveReports { get; set; }
    public DbSet<ApplicantSearchProjection> ApplicantSearchProjections { get; set; }
    public DbSet<PackageDetails> PackageDetails { get; set; }
    public DbSet<ClientDetails> ClientDetails { get; set; }
    public DbSet<RoleDetails> RoleDetails { get; set; }
    public DbSet<ModuleDetails> ModuleDetails { get; set; }
	public DbSet<UserDetails> UserDetails { get; set; }
	public DbSet<UserClientDetails> UserClientDetails { get; set; }
	public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ATSDBContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
