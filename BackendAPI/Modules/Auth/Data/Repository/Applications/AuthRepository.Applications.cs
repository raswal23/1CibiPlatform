namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<PaginatedResult<ApplicationsDTO>> GetApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var usersQuery = _dbcontext
				.AuthApplications
				.AsNoTracking()
				.Where(aa => aa.IsActive);
	
			var totalRecords = await usersQuery.LongCountAsync(cancellationToken);
	
			var applications = await usersQuery
							.OrderBy(a => a.AppId)
							.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
							.Take(paginationRequest.PageSize)
							.Select(aa => new ApplicationsDTO(
								aa.AppId,
								aa.AppName,
								aa.Description ?? "",
								aa.IsActive))
							.ToListAsync(cancellationToken);
	
			return new PaginatedResult<ApplicationsDTO>
			(
				paginationRequest.PageIndex,
				paginationRequest.PageSize,
				totalRecords,
				applications
			);
		}
	
	public async Task<PaginatedResult<ApplicationsDTO>> SearchApplicationsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
		{
			var applicationsQuery = _dbcontext.AuthApplications
					.AsNoTracking()
					.Where(au => au.IsActive &&
						(EF.Functions.ILike(au.AppName, $"%{paginationRequest.SearchTerm}%") ||
						 EF.Functions.ILike(au.Description!, $"%{paginationRequest.SearchTerm}%")));
	
			var totalRecords = await applicationsQuery.CountAsync(cancellationToken);
	
			var applications = await applicationsQuery
								.OrderBy(asm => asm.AppId)
								.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
								.Take(paginationRequest.PageSize)
								.Select(asm => new ApplicationsDTO(
									asm.AppId,
									asm.AppName,
									asm.Description ?? "",
									asm.IsActive))
								.ToListAsync(cancellationToken);
	
			return new PaginatedResult<ApplicationsDTO>
				(
				  paginationRequest.PageIndex,
				  paginationRequest.PageSize,
				  totalRecords,
				  applications
				);
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
