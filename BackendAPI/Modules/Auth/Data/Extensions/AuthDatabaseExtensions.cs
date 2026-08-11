namespace Auth.Data.Extensions;

public static class AuthDatabaseExtensions
{
	public static async Task AuthIntializeDatabaseAsync(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<AuthApplicationDbContext>();
		var initData = scope.ServiceProvider.GetRequiredService<AuthInitialData>();
		context.Database.MigrateAsync().GetAwaiter().GetResult();
		await SeedAsync(context, initData);
	}
	private static async Task SeedAsync(
		AuthApplicationDbContext context,
		AuthInitialData initData)
	{
		await SeedUser(context, initData);
	}
	private static async Task SeedUser(
		AuthApplicationDbContext context,
		AuthInitialData initData)
	{
		if (!await context.AuthUsers.AnyAsync())
		{
			await context.AuthUsers.AddRangeAsync(initData.GetUsers());
		}
		if (!await context.AuthApplications.AnyAsync())
		{
			await context.AuthApplications.AddRangeAsync(initData.GetApplications());
		}
		if (!await context.AuthRoles.AnyAsync())
		{
			await context.AuthRoles.AddRangeAsync(initData.GetRoles());
		}
		if (!await context.AuthSubmenu.AnyAsync())
		{
			await context.AuthSubmenu.AddRangeAsync(initData.GetSubMenus());
		}
		await context.SaveChangesAsync();

		// The rows above carry explicit keys, so the identity sequences still point at
		// their old values and would hand out keys that are already taken.
		await SyncIdentitySequences(context);

		// The assignments reference the rows above, so they can only be built once
		// those rows are persisted and their keys are known.
		await SeedUserAppRoles(context, initData);
	}

	private static async Task SyncIdentitySequences(AuthApplicationDbContext context)
	{
		await context.Database.ExecuteSqlRawAsync(
			"""
			SELECT setval(
				pg_get_serial_sequence('"AuthApplications"', 'AppId'),
				GREATEST((SELECT MAX("AppId") FROM "AuthApplications"), 1),
				TRUE);

			SELECT setval(
				pg_get_serial_sequence('"AuthRoles"', 'RoleId'),
				GREATEST((SELECT MAX("RoleId") FROM "AuthRoles"), 1),
				TRUE);

			SELECT setval(
				pg_get_serial_sequence('"AuthSubmenu"', 'SubMenuId'),
				GREATEST((SELECT MAX("SubMenuId") FROM "AuthSubmenu"), 1),
				TRUE);
			""");
	}

	private static async Task SeedUserAppRoles(
		AuthApplicationDbContext context,
		AuthInitialData initData)
	{
		if (await context.AuthUserAppRoles.AnyAsync())
		{
			return;
		}

		var userIdsByEmail = await context.AuthUsers
			.ToDictionaryAsync(user => user.Email, user => user.Id);

		// The super admin owns every assignment, so nothing can be assigned without it.
		if (!userIdsByEmail.ContainsKey(AuthInitialData.SuperAdminEmail))
		{
			return;
		}

		var appIdsByName = await context.AuthApplications
			.ToDictionaryAsync(app => app.AppName, app => app.AppId);
		var subMenuIdsByName = await context.AuthSubmenu
			.ToDictionaryAsync(subMenu => subMenu.SubMenuName, subMenu => subMenu.SubMenuId);
		var roleIdsByName = await context.AuthRoles
			.ToDictionaryAsync(role => role.RoleName, role => role.RoleId);

		await context.AuthUserAppRoles.AddRangeAsync(initData.GetUserAppRoles(
			userIdsByEmail[AuthInitialData.SuperAdminEmail],
			userIdsByEmail,
			appIdsByName,
			subMenuIdsByName,
			roleIdsByName));

		await context.SaveChangesAsync();
	}
}
