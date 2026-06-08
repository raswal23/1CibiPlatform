namespace Auth.Data.DataSeed;

public class AuthInitialData
{

	private readonly IPasswordHasherService _passwordHasherService;
	private readonly Guid _Id;

	public AuthInitialData(IPasswordHasherService passwordHasherService)
	{
		this._passwordHasherService = passwordHasherService;
		this._Id = Guid.CreateVersion7();
	}
	public IEnumerable<Authusers> GetUsers()
	{
		return new List<Authusers>
			{
				new Authusers
				{
					Id = this._Id,
					Email = "admin@cibi.com",
					PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
					FirstName = "Super",
					LastName = "Admin",
					IsApproved = true
				}
			};
	}

	public IEnumerable<AuthUserAppRole> GetUserAppRoles()
	{
		return new List<AuthUserAppRole>
			{
				new AuthUserAppRole
				{
					UserId = this._Id,
					AppId = 1,
					Submenu = 1,
					RoleId = 1,
					AssignedBy = this._Id
				},
				new AuthUserAppRole
				{
					UserId = this._Id,
					AppId = 2,
					Submenu= 2,
					RoleId = 2,
					AssignedBy = this._Id
				},
				new AuthUserAppRole
				{
					UserId = this._Id,
					AppId = 3,
					Submenu= 3,
					RoleId = 3,
					AssignedBy = this._Id
				}
			};
	}

	public IEnumerable<AuthApplication> GetApplications()
	{
		return new List<AuthApplication>
			{
				new AuthApplication
				{
					AppName = "CNX",
					Description = "Concentrix API"
				},
				new AuthApplication
				{
					AppName = "Philsys",
					Description = "IDV"
				},
				new AuthApplication
				{
					AppName = "Settings",
					Description = "OnePlatform Settings"
				},
				new AuthApplication
				{
					AppName = "AI",
					Description = "AI"
				},
				new AuthApplication
				{
					AppName = "Credit Bureau",
					Description = "Credit Bureau"
				},
				new AuthApplication
				{
					AppName = "S&I",
					Description = "S&I"
				}
			};
	}


	public IEnumerable<AuthRole> GetRoles()
	{
		return new List<AuthRole>
			{
				new AuthRole
				{
					RoleName = "SuperAdmin",
					Description = "Super Admin"
				},
				new AuthRole
				{
					RoleName = "Admin",
					Description = "Administrator Role"
				},
				new AuthRole
				{
					RoleName = "User",
					Description = "User Role"
				}
			};
	}

	//create for sub menu
	public IEnumerable<AuthSubMenu> GetSubMenus()
	{
		return new List<AuthSubMenu>
			{
				new AuthSubMenu
				{
					SubMenuName = "CNX Dashboard",
					Description = "List of Subjects"
				},
				new AuthSubMenu
				{
					SubMenuName = "IDV",
					Description = "Philsys IDV"
				},
				new AuthSubMenu
				{
					SubMenuName = "User Management",
					Description = "Assigning of Application, SubMenus, and Roles"
				},
				new AuthSubMenu
				{
					SubMenuName = "Chat",
					Description = "Chat"
				},
				new AuthSubMenu
				{
					SubMenuName = "CB 2.0",
					Description = "CB 2.0"
				},
				new AuthSubMenu
				{
					SubMenuName = "Bulk Processing",
					Description = "Bulk Processing"
				},
				new AuthSubMenu
				{
					SubMenuName = "ATS",
					Description = "ATS"
				}
			};
	}

}
