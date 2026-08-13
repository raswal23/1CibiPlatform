namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<LoginDTO> GetUserDataAsync(LoginWebCred cred)
		{
			var userData = await (from user in _dbcontext.AuthUsers
								  join userRole in _dbcontext.AuthUserAppRoles
															 on user.Id equals userRole.UserId into userRolesGroup
								  where user.Email == cred.Username && user.IsActive == true
								  select new LoginDTO(
								   user.Id,
								   user.PasswordHash,
								   user.Email!,
								   user.FirstName!,
								   user.LastName!,
								   user.MiddleName,
								   user.IsApproved,
								   userRolesGroup.Select(r => r.AppId).Distinct().ToList(),
								   userRolesGroup.GroupBy(r => r.AppId)
												 .Select(g => g.Select(r => r.Submenu).ToList())
												 .ToList(),
								   userRolesGroup.Select(r => r.RoleId).Distinct().ToList()
								  ))
								 .AsNoTracking()
								 .FirstOrDefaultAsync();
	
			return userData!;
		}
	
	public async Task<bool> SaveUserAsync(Authusers user)
		{
			await _dbcontext.AuthUsers.AddAsync(user);
	
			var result = await _dbcontext.SaveChangesAsync();
	
			return true;
		}
}
