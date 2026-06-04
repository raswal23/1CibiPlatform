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
	public DbSet<SignatureDetails> SignatureDetails { get; set; }
	public DbSet<DocumentDetails> DocumentDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ATSDBContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
