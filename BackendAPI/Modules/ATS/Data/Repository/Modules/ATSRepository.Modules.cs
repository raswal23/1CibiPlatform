namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset ordered by ModuleName (unique index) — pure query; the service decodes
	// the cursor and mints the next one.
	public async Task<List<ModuleDetailsDTO>> GetModulesPageAsync(string? searchTerm, string? afterModuleName, int take, CancellationToken cancellationToken)
	{
		var query = BuildModulesQuery(searchTerm);
		if (afterModuleName is not null)
			query = query.Where(module => string.Compare(module.ModuleName, afterModuleName) > 0);

		return await query.OrderBy(module => module.ModuleName).Take(take)
			.Select(module => new ModuleDetailsDTO
			{
				ModuleId = module.ModuleId,
				ModuleName = module.ModuleName,
				ModuleDescription = module.ModuleDescription,
				IsActive = module.IsActive,
				CreatedAt = module.CreatedAt,
				UpdatedAt = module.UpdatedAt
			}).ToListAsync(cancellationToken);
	}

	public Task<long> CountModulesAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildModulesQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<ModuleDetails> BuildModulesQuery(string? searchTerm)
	{
		var query = _dbcontext.ModuleDetails.AsNoTracking();
		if (!string.IsNullOrEmpty(searchTerm))
			query = query.Where(module => EF.Functions.ILike(module.ModuleName, $"%{searchTerm}%") || EF.Functions.ILike(module.ModuleDescription, $"%{searchTerm}%"));
		return query;
	}

	public async Task<bool> AddModuleAsync(AddModuleDTO dto)
	{
		var moduleName = dto.ModuleName!.Trim();
		var now = DateTime.UtcNow;
		await _dbcontext.ModuleDetails.AddAsync(new ModuleDetails
		{
			ModuleName = moduleName,
			ModuleDescription = dto.ModuleDescription!.Trim(),
			IsActive = dto.IsActive,
			CreatedAt = now,
			UpdatedAt = now
		});
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public Task<ModuleDetails?> GetModuleAsync(int moduleId) =>
		_dbcontext.ModuleDetails.AsNoTracking().FirstOrDefaultAsync(module => module.ModuleId == moduleId);

	public Task<bool> ModuleNameExistsAsync(string moduleName)
	{
		var normalizedModuleName = moduleName.Trim().ToUpperInvariant();
		return _dbcontext.ModuleDetails.AsNoTracking()
			.AnyAsync(module => module.ModuleName.Trim().ToUpper() == normalizedModuleName);
	}

	public async Task<ModuleDetails> EditModuleAsync(ModuleDetails module)
	{
		_dbcontext.ModuleDetails.Update(module);
		await _dbcontext.SaveChangesAsync();
		return module;
	}
}
