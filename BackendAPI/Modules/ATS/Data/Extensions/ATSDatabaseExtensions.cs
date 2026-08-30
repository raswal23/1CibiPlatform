namespace ATS.Data.Extensions;

public static class ATSDatabaseExtensions
{
	public static async Task ATSIntializeDatabaseAsync(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();

		var context = scope.ServiceProvider.GetRequiredService<ATSDBContext>();
		var initData = scope.ServiceProvider.GetRequiredService<ATSInitialData>();
		var authQueries = scope.ServiceProvider.GetRequiredService<IAuthQueries>();

		await context.Database.MigrateAsync();
		await SeedAsync(context, initData, authQueries);

		await InitializeQuartzAsync(context);
	}

	private static async Task SeedAsync(
		ATSDBContext context,
		ATSInitialData initData,
		IAuthQueries authQueries)
	{

		if (!await context.EmailInvitationRequests.AsNoTracking().AnyAsync())
		{

			var userIdsByEmail = await authQueries.GetUserIdsByEmailAsync(
			ATSInitialData.GetATSUserEmails().ToArray(),
			CancellationToken.None);

			// Orders carry a foreign key to their package. The rows come from the
			// legacy migration rather than this seed, so the map is read from the
			// database and a seed row whose package is absent is skipped.
			var packageIdsByName = await context.PackageDetails
				.AsNoTracking()
				.ToDictionaryAsync(package => package.PackageName, package => package.PackageId);

			await context.EmailInvitationRequests.AddRangeAsync(
				initData.GetEmailInvitationRequests(userIdsByEmail, packageIdsByName));
		}


		if (!await context.RoleDetails.AnyAsync())
		{
			await context.RoleDetails.AddRangeAsync
			(initData.GetATSRoles());
		}

		// Migrations may have pre-seeded part of this table (SeedATSSuperAdminAccess
		// inserts modules 1-10), so an emptiness check would skip the remaining modules
		// and the user seed below would then violate FK_UserDetails_ModuleDetails_ModuleId.
		var existingModuleIds = await context.ModuleDetails
			.AsNoTracking()
			.Select(module => module.ModuleId)
			.ToListAsync();

		await context.ModuleDetails.AddRangeAsync(
			initData.GetATSModules()
				.Where(module => !existingModuleIds.Contains(module.ModuleId)));

		if (!await context.UserDetails.AnyAsync())
		{
			var userIdsByEmail = await authQueries.GetUserIdsByEmailAsync(
				ATSInitialData.GetATSUserEmails().ToArray(),
				CancellationToken.None);

			await context.UserDetails.AddRangeAsync(
				initData.GetATSUsers(userIdsByEmail));
		}

		await context.SaveChangesAsync();

		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.BulkUploads);
		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.TicketingStatus);
	}

	// The seed blocks above only run on an empty table, so a module added after the
	// first deployment would never reach an existing database. This backfills one such
	// module and grants it to everyone who can already reach New Order, which is the
	// access rule these monitoring modules follow. Idempotent: a second run adds nothing.
	private static async Task BackfillModuleGrantedWithNewOrderAsync(
		ATSDBContext context,
		ATSInitialData initData,
		int moduleId)
	{
		var moduleExists = await context.ModuleDetails
			.AnyAsync(module => module.ModuleId == moduleId);

		if (!moduleExists)
		{
			var module = initData.GetATSModules()
				.FirstOrDefault(candidate => candidate.ModuleId == moduleId);

			if (module is null)
			{
				return;
			}

			await context.ModuleDetails.AddAsync(module);
			await context.SaveChangesAsync();
		}

		// One access row per user per module, so the grant is modelled as a copy of the
		// user's New Order row with the module id swapped.
		var newOrderRows = await context.UserDetails
			.AsNoTracking()
			.Where(user => user.ModuleId == AtsModuleIds.NewOrder)
			.ToListAsync();

		if (newOrderRows.Count == 0)
		{
			return;
		}

		var alreadyGranted = await context.UserDetails
			.AsNoTracking()
			.Where(user => user.ModuleId == moduleId)
			.Select(user => user.UserId)
			.ToListAsync();

		var grantedUserIds = alreadyGranted.ToHashSet();

		var newRows = newOrderRows
			.Where(user => !grantedUserIds.Contains(user.UserId))
			.Select(user => new UserDetails
			{
				UserId = user.UserId,
				UserEmail = user.UserEmail,
				UserName = user.UserName,
				RoleId = user.RoleId,
				ClientId = user.ClientId,
				Site = user.Site,
				IsActive = user.IsActive,
				ModuleId = moduleId,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			})
			.ToList();

		if (newRows.Count == 0)
		{
			return;
		}

		await context.UserDetails.AddRangeAsync(newRows);
		await context.SaveChangesAsync();
	}

	private static async Task InitializeQuartzAsync(ATSDBContext context)
	{
		await using var connection = new NpgsqlConnection(
			context.Database.GetConnectionString());

		await connection.OpenAsync();

		// Check if Quartz is already initialized
		const string checkSql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema='ats'
                AND table_name='qrtz_job_details'
            );
            """;

		await using var checkCommand = new NpgsqlCommand(checkSql, connection);

		var exists = (bool)(await checkCommand.ExecuteScalarAsync())!;

		if (exists)
			return;

		var scriptPath = System.IO.Path.Combine(
			AppContext.BaseDirectory,
			"Scripts",
			"quartz_postgres.sql");

		var sql = await File.ReadAllTextAsync(scriptPath);

		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}
}
