namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Keyset over the grouped projection (UserName, UserEmail, UserId): one page is
	// take-1 logical users, each expanding to a row per module via GetUsersByIdsAsync.
	// The service mints the cursor from the last key returned here.
	public async Task<List<UserPageKeyDTO>> GetUserPageKeysAsync(string? searchTerm, int? clientId, string? afterUserName, string? afterUserEmail, Guid? afterUserId, int take, CancellationToken cancellationToken)
	{
		var logical = BuildLogicalUsersQuery(searchTerm, clientId);
		if (afterUserName is not null && afterUserEmail is not null && afterUserId.HasValue)
		{
			var cId = afterUserId.Value;
			logical = logical.Where(user =>
				string.Compare(user.UserName, afterUserName) > 0
				|| (user.UserName == afterUserName && (string.Compare(user.UserEmail, afterUserEmail) > 0
				|| (user.UserEmail == afterUserEmail && user.UserId.CompareTo(cId) > 0))));
		}

		return await logical
			.OrderBy(user => user.UserName).ThenBy(user => user.UserEmail).ThenBy(user => user.UserId)
			.Take(take)
			.ToListAsync(cancellationToken);
	}

	public Task<List<UserDetailsDTO>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, string? searchTerm, int? clientId, CancellationToken cancellationToken) =>
		BuildUsersQuery(searchTerm, clientId).Where(user => userIds.Contains(user.UserId))
			.OrderBy(user => user.UserName).ThenBy(user => user.UserEmail)
			.ThenBy(user => user.UserId).ThenBy(user => user.ModuleId).Select(user => new UserDetailsDTO
			{
				UserId = user.UserId,
				UserName = user.UserName,
				UserEmail = user.UserEmail,
				IsActive = user.IsActive,
				ClientId = user.ClientId,
				Site = user.Site,
				RoleId = user.RoleId,
				ModuleId = user.ModuleId,
				CreatedAt = user.CreatedAt,
				UpdatedAt = user.UpdatedAt
			}).ToListAsync(cancellationToken);

	public Task<long> CountUsersAsync(string? searchTerm, int? clientId, CancellationToken cancellationToken) =>
		BuildLogicalUsersQuery(searchTerm, clientId).LongCountAsync(cancellationToken);

	private IQueryable<UserDetails> BuildUsersQuery(string? searchTerm, int? clientId)
	{
		var query = _dbcontext.UserDetails.AsNoTracking();
		if (clientId.HasValue)
			query = query.Where(user => user.ClientId == clientId.Value);
		if (!string.IsNullOrEmpty(searchTerm))
		{
			var term = $"%{searchTerm}%";
			query = query.Where(user => EF.Functions.ILike(user.UserName, term) || EF.Functions.ILike(user.UserEmail, term) || EF.Functions.ILike(user.Site, term));
		}
		return query;
	}

	private IQueryable<UserPageKeyDTO> BuildLogicalUsersQuery(string? searchTerm, int? clientId) =>
		BuildUsersQuery(searchTerm, clientId).GroupBy(user => user.UserId).Select(group => new UserPageKeyDTO
		{
			UserId = group.Key,
			UserName = group.Min(user => user.UserName),
			UserEmail = group.Min(user => user.UserEmail)
		});

	public async Task<bool> AddUserAsync(IReadOnlyCollection<AddUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var users = userDTOs.ToArray();
		var user = users[0];
		var email = user.UserEmail.Trim();
		var moduleIds = users.Select(item => item.ModuleId).Distinct().ToArray();

		var now = DateTime.UtcNow;
		if (user.ClientId.HasValue && !await _dbcontext.UserClientDetails.AsNoTracking().AnyAsync(item => item.UserId == user.UserId, cancellationToken))
			await _dbcontext.UserClientDetails.AddAsync(new UserClientDetails { UserId = user.UserId, ClientId = user.ClientId.Value, CreatedAt = now, UpdatedAt = now }, cancellationToken);

		await _dbcontext.UserDetails.AddRangeAsync(moduleIds.Select(moduleId => new UserDetails
		{
			UserId = user.UserId,
			UserName = user.UserName.Trim(),
			UserEmail = email,
			IsActive = user.IsActive,
			ClientId = user.ClientId,
			Site = user.Site.Trim(),
			RoleId = user.RoleId,
			ModuleId = moduleId,
			CreatedAt = now,
			UpdatedAt = now
		}), cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<IReadOnlyList<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
		await _dbcontext.UserDetails.AsNoTracking().Where(user => user.UserId == userId).OrderBy(user => user.ModuleId).ToListAsync(cancellationToken);

	public Task<bool> UserExistsAsync(Guid userId, string email, CancellationToken cancellationToken) =>
		_dbcontext.UserDetails.AsNoTracking().AnyAsync(user => user.UserId == userId || EF.Functions.ILike(user.UserEmail, email), cancellationToken);

	public Task<bool> UserEmailExistsAsync(Guid userId, string email, CancellationToken cancellationToken) =>
		_dbcontext.UserDetails.AsNoTracking().AnyAsync(user => user.UserId != userId && EF.Functions.ILike(user.UserEmail, email), cancellationToken);

	public Task<bool> RoleIsActiveAsync(int roleId, CancellationToken cancellationToken) =>
		_dbcontext.RoleDetails.AsNoTracking().AnyAsync(role => role.RoleId == roleId && role.IsActive, cancellationToken);

	public Task<int> CountActiveModulesAsync(IReadOnlyCollection<int> moduleIds, CancellationToken cancellationToken) =>
		_dbcontext.ModuleDetails.AsNoTracking().CountAsync(module => moduleIds.Contains(module.ModuleId) && module.IsActive, cancellationToken);

	public async Task<IReadOnlyList<int>> GetActiveUserRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
		await _dbcontext.UserDetails.AsNoTracking()
			.Where(user => user.UserId == userId && user.IsActive && user.Role.IsActive)
			.Select(user => user.RoleId)
			.Distinct()
			.OrderBy(roleId => roleId)
			.ToListAsync(cancellationToken);

	public async Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
		await _dbcontext.UserDetails.AsNoTracking().Where(user => user.UserId == userId && user.IsActive && user.Module.IsActive)
			.Select(user => user.ModuleId).Distinct().OrderBy(moduleId => moduleId).ToListAsync(cancellationToken);

	public async Task<IReadOnlyList<UserDetails>> EditUserAsync(IReadOnlyCollection<EditUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var users = userDTOs.ToArray();
		var userId = users[0].UserId;
		var existing = await _dbcontext.UserDetails.Where(user => user.UserId == userId).ToListAsync(cancellationToken);

		var user = users[0];
		var email = user.UserEmail.Trim();
		var selectedModuleIds = users.Select(item => item.ModuleId).Distinct().ToHashSet();
		var existingModuleIds = existing.Select(item => item.ModuleId).ToHashSet();
		var newModuleIds = selectedModuleIds.Except(existingModuleIds).ToArray();

		var now = DateTime.UtcNow;
		var createdAt = existing.Min(item => item.CreatedAt);
		var name = user.UserName.Trim();
		var site = user.Site.Trim();
		_dbcontext.UserDetails.RemoveRange(existing.Where(item => !selectedModuleIds.Contains(item.ModuleId)));
		foreach (var item in existing.Where(item => selectedModuleIds.Contains(item.ModuleId)))
		{
			item.UserName = name; item.UserEmail = email; item.IsActive = user.IsActive; item.ClientId = user.ClientId;
			item.Site = site; item.RoleId = user.RoleId; item.UpdatedAt = now;
		}
		var added = newModuleIds.Select(moduleId => new UserDetails
		{
			UserId = userId,
			UserName = name,
			UserEmail = email,
			IsActive = user.IsActive,
			ClientId = user.ClientId,
			Site = site,
			RoleId = user.RoleId,
			ModuleId = moduleId,
			CreatedAt = createdAt,
			UpdatedAt = now
		}).ToArray();
		await _dbcontext.UserDetails.AddRangeAsync(added, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return existing.Where(item => selectedModuleIds.Contains(item.ModuleId)).Concat(added).OrderBy(item => item.ModuleId).ToArray();
	}
}
