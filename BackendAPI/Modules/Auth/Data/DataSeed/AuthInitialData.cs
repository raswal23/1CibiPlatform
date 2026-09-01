namespace Auth.Data.DataSeed;

public class AuthInitialData
{
	public const string SuperAdminEmail = "admin@cibi.com";

	private readonly IPasswordHasherService _passwordHasherService;

	public AuthInitialData(IPasswordHasherService passwordHasherService)
	{
		this._passwordHasherService = passwordHasherService;
	}
	public IEnumerable<Authusers> GetUsers()
	{
		return new List<Authusers>
			{
				new Authusers
				{
					Id = Guid.CreateVersion7(),
					Email = SuperAdminEmail,
					PasswordHash = _passwordHasherService.HashPassword("p@ssw0rd!"),
					FirstName = "Super",
					LastName = "Admin",
					IsApproved = true
				},
				new Authusers
				{
					Id = Guid.CreateVersion7(),
					Email = "atsManager@cibi.com",
					PasswordHash = _passwordHasherService.HashPassword("managerP@ss*"),
					FirstName = "ATS",
					LastName = "Platform Manager",
					IsApproved = true
				},
				new Authusers
				{
					Id = Guid.CreateVersion7(),
					Email = "atsAdmin@cibi.com",
					PasswordHash = _passwordHasherService.HashPassword("adminP@ss*"),
					FirstName = "ATS",
					LastName = "Admin",
					IsApproved = true
				},
				new Authusers
				{
					Id = Guid.CreateVersion7(),
					Email = "atsService@cibi.com",
					PasswordHash = _passwordHasherService.HashPassword("serviceP@ss*"),
					FirstName = "ATS",
					LastName = "Service Delivery",
					IsApproved = true
				},
				new Authusers
				{
					Id = Guid.CreateVersion7(),
					Email = "atsUser@cibi.com",
					PasswordHash = _passwordHasherService.HashPassword("userP@ss*"),
					FirstName = "ATS",
					LastName = "User",
					IsApproved = true
				},
			};
	}

	private static readonly (string Email, string AppName, string SubMenuName, string RoleName)[] UserAssignments =
	[
		(SuperAdminEmail, "CNX", "CNX Dashboard", "SuperAdmin"),
		(SuperAdminEmail, "Philsys", "IDV", "Admin"),
		(SuperAdminEmail, "Settings", "User Management", "User"),
		(SuperAdminEmail, "S&I", "ATS", "User"),


		("atsManager@cibi.com", "S&I", "ATS", "User"),
		("atsAdmin@cibi.com", "S&I", "ATS", "User"),
		("atsService@cibi.com", "S&I", "ATS", "User"),
		("atsUser@cibi.com", "S&I", "ATS", "User")
	];

	public IEnumerable<AuthUserAppRole> GetUserAppRoles(
		Guid superAdminUserId,
		IReadOnlyDictionary<string, Guid> userIdsByEmail,
		IReadOnlyDictionary<string, int> appIdsByName,
		IReadOnlyDictionary<string, int> subMenuIdsByName,
		IReadOnlyDictionary<string, int> roleIdsByName)
	{
		return UserAssignments
			.Select(assignment => new AuthUserAppRole
			{
				UserId = ResolveUserId(userIdsByEmail, assignment.Email),
				AppId = ResolveId(appIdsByName, assignment.AppName, "application"),
				Submenu = ResolveId(subMenuIdsByName, assignment.SubMenuName, "sub menu"),
				RoleId = ResolveId(roleIdsByName, assignment.RoleName, "role"),
				AssignedBy = superAdminUserId
			})
			.ToList();
	}

	private static Guid ResolveUserId(
		IReadOnlyDictionary<string, Guid> userIdsByEmail,
		string email)
	{
		if (!userIdsByEmail.TryGetValue(email, out var userId))
		{
			throw new InvalidOperationException(
				$"Seeded user '{email}' was not found, so the user assignments cannot be created.");
		}

		return userId;
	}

	private static int ResolveId(
		IReadOnlyDictionary<string, int> idsByName,
		string name,
		string description)
	{
		if (!idsByName.TryGetValue(name, out var id))
		{
			throw new InvalidOperationException(
				$"Seeded {description} '{name}' was not found, so the user assignments cannot be created.");
		}

		return id;
	}

	public IEnumerable<AuthApplication> GetApplications()
	{
		return new List<AuthApplication>
			{
				new AuthApplication
				{
					AppId = 1,
					AppName = "CNX",
					Description = "Concentrix API"
				},
				new AuthApplication
				{
					AppId = 2,
					AppName = "Philsys",
					Description = "IDV"
				},
				new AuthApplication
				{
					AppId = 3,
					AppName = "Settings",
					Description = "OnePlatform Settings"
				},
				new AuthApplication
				{
					AppId = 4,
					AppName = "AI",
					Description = "AI"
				},
				new AuthApplication
				{
					AppId = 5,
					AppName = "Credit Bureau",
					Description = "Credit Bureau"
				},
				new AuthApplication
				{
					AppId = 6,
					AppName = "S&I",
					Description = "S&I"
				},
				new AuthApplication
				{
					AppId = 7,
					AppName = "Administration",
					Description = "Administration"
				},
				new AuthApplication
				{
					AppId = 8,
					AppName = "Employment Verification",
					Description = "Employment Verification"
				}
			};
	}


	public IEnumerable<AuthRole> GetRoles()
	{
		return new List<AuthRole>
			{
				new AuthRole
				{
					RoleId = 1,
					RoleName = "SuperAdmin",
					Description = "Super Admin"
				},
				new AuthRole
				{
					RoleId = 2,
					RoleName = "Admin",
					Description = "Administrator Role"
				},
				new AuthRole
				{
					RoleId = 3,
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
					SubMenuId = 1,
					SubMenuName = "CNX Dashboard",
					Description = "List of Subjects"
				},
				new AuthSubMenu
				{
					SubMenuId = 2,
					SubMenuName = "IDV",
					Description = "Philsys IDV"
				},
				new AuthSubMenu
				{
					SubMenuId = 3,
					SubMenuName = "User Management",
					Description = "Assigning of Application, SubMenus, and Roles"
				},
				new AuthSubMenu
				{
					SubMenuId = 4,
					SubMenuName = "Chat",
					Description = "Chat"
				},
				new AuthSubMenu
				{
					SubMenuId = 5,
					SubMenuName = "CB 2.0",
					Description = "CB 2.0"
				},
				new AuthSubMenu
				{
					SubMenuId = 6,
					SubMenuName = "Bulk Processing",
					Description = "Bulk Processing"
				},
				new AuthSubMenu
				{
					SubMenuId = 7,
					SubMenuName = "ATS",
					Description = "ATS"
				},
				new AuthSubMenu
				{
					SubMenuId = 8,
					SubMenuName = "logs",
					Description = "logs"
				},
				new AuthSubMenu
				{
					SubMenuId = 9,
					SubMenuName = "verification",
					Description = "verification"
				}
			};
	}

}
