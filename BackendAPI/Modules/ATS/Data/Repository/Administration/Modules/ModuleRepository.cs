namespace ATS.Data.Repository.Administration.Modules;

public sealed class ModuleRepository : IModuleRepository
{
	private readonly ATSDBContext _dbContext;

	public ModuleRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(PaginationRequest request, CancellationToken cancellationToken) => GetPageAsync(request, false, cancellationToken);
	public Task<PaginatedResult<ModuleDetailsDTO>> SearchModulesAsync(PaginationRequest request, CancellationToken cancellationToken) => GetPageAsync(request, true, cancellationToken);

	public async Task<bool> AddModuleAsync(AddModuleDTO dto)
	{
		var now = DateTime.UtcNow;
		await _dbContext.ModuleDetails.AddAsync(new ModuleDetails
		{
			ModuleName = dto.ModuleName!, ModuleDescription = dto.ModuleDescription!, IsActive = dto.IsActive,
			CreatedAt = now, UpdatedAt = now
		});
		await _dbContext.SaveChangesAsync();
		return true;
	}

	public Task<ModuleDetails?> GetModuleAsync(int moduleId) =>
		_dbContext.ModuleDetails.AsNoTracking().FirstOrDefaultAsync(module => module.ModuleId == moduleId);

	public async Task<ModuleDetails> EditModuleAsync(ModuleDetails module)
	{
		_dbContext.ModuleDetails.Update(module);
		await _dbContext.SaveChangesAsync();
		return module;
	}

	private async Task<PaginatedResult<ModuleDetailsDTO>> GetPageAsync(PaginationRequest request, bool search, CancellationToken cancellationToken)
	{
		var query = _dbContext.ModuleDetails.AsNoTracking();
		if (search)
			query = query.Where(module => EF.Functions.ILike(module.ModuleName, $"%{request.SearchTerm}%") || EF.Functions.ILike(module.ModuleDescription, $"%{request.SearchTerm}%"));
		var count = await query.CountAsync(cancellationToken);
		var items = await query.OrderBy(module => module.ModuleName).Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
			.Select(module => new ModuleDetailsDTO
			{
				ModuleId = module.ModuleId, ModuleName = module.ModuleName, ModuleDescription = module.ModuleDescription,
				IsActive = module.IsActive, CreatedAt = module.CreatedAt, UpdatedAt = module.UpdatedAt
			}).ToListAsync(cancellationToken);
		return new PaginatedResult<ModuleDetailsDTO>(request.PageIndex, request.PageSize, count, items);
	}
}
