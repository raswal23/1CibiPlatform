namespace ATS.Data.EntityConfiguration;

public class ModuleDetailsConfiguration : IEntityTypeConfiguration<ModuleDetails>
{
	public void Configure(EntityTypeBuilder<ModuleDetails> builder)
	{
		builder.ToTable("ModuleDetails", "ats");

		builder.HasKey(x => x.ModuleId);

		builder.Property(x => x.ModuleId)
			.IsRequired()
			.ValueGeneratedOnAdd();

		builder.Property(x => x.ModuleName)
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(x => x.ModuleDescription)
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.Property(x => x.UpdatedAt)
			.IsRequired();

		builder.HasIndex(x => x.ModuleName)
			.IsUnique();
	}
}
