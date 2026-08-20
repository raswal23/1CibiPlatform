namespace Auth.Data.Repository;

public partial class AuthRepository
{
	// Keyset page over AuthApplications ordered by AppId (unique PK). Pure query —
	// the service decodes the cursor and mints the next one.
	public async Task<List<ApplicationsDTO>> GetApplicationsPageAsync(string? searchTerm, int? afterAppId, int take, CancellationToken cancellationToken)
		{
			var applicationsQuery = BuildApplicationsQuery(searchTerm);
			if (afterAppId.HasValue)
				applicationsQuery = applicationsQuery.Where(aa => aa.AppId > afterAppId.Value);

			return await applicationsQuery
							.OrderBy(a => a.AppId)
							.Take(take)
							.Select(aa => new ApplicationsDTO(
								aa.AppId,
								aa.AppName,
								aa.Description ?? "",
								aa.IsActive))
							.ToListAsync(cancellationToken);
		}

	public Task<long> CountApplicationsAsync(string? searchTerm, CancellationToken cancellationToken) =>
		BuildApplicationsQuery(searchTerm).LongCountAsync(cancellationToken);

	private IQueryable<AuthApplication> BuildApplicationsQuery(string? searchTerm)
	{
		var applicationsQuery = _dbcontext.AuthApplications
			.AsNoTracking()
			.Where(aa => aa.IsActive);

		if (!string.IsNullOrEmpty(searchTerm))
			applicationsQuery = applicationsQuery.Where(aa =>
				EF.Functions.ILike(aa.AppName, $"%{searchTerm}%") ||
				EF.Functions.ILike(aa.Description!, $"%{searchTerm}%"));

		return applicationsQuery;
	}

	public async Task<AuthApplication> GetApplicationAsync(int applicationId)
		{
			var application = await _dbcontext.AuthApplications
			.FirstOrDefaultAsync(x => x.AppId == applicationId);

			return application!;
		}

	public async Task<bool> DeleteApplicationAsync(AuthApplication application)
		{

			var isDeleted = _dbcontext.AuthApplications.Remove(application);
			await _dbcontext.SaveChangesAsync();
			return true;
		}

	public async Task<bool> AddApplicationAsync(AddApplicationDTO applications)
		{
			var addedApplication = new AuthApplication
			{
				AppName = applications.AppName!,
				Description = applications.Description,
				IsActive = applications.IsActive,
				CreatedAt = DateTime.UtcNow
			};
			var isAdded = await _dbcontext.AuthApplications.AddAsync(addedApplication);
			await _dbcontext.SaveChangesAsync();
			return true;
		}

	public async Task<AuthApplication> EditApplicationAsync(AuthApplication application)
		{
			_dbcontext.AuthApplications.Update(application);
			await _dbcontext.SaveChangesAsync();

			return application;
		}
}
