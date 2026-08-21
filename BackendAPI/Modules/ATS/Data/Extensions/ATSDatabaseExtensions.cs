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

			await context.EmailInvitationRequests.AddRangeAsync(
				initData.GetEmailInvitationRequests(userIdsByEmail));
		}


		if (!await context.RoleDetails.AnyAsync())
		{
			await context.RoleDetails.AddRangeAsync
			(initData.GetATSRoles());
		}

		if (!await context.ModuleDetails.AnyAsync())
		{
			await context.ModuleDetails.AddRangeAsync
			(initData.GetATSModules());
		}

		if (!await context.UserDetails.AnyAsync())
		{
			var userIdsByEmail = await authQueries.GetUserIdsByEmailAsync(
				ATSInitialData.GetATSUserEmails().ToArray(),
				CancellationToken.None);

			await context.UserDetails.AddRangeAsync(
				initData.GetATSUsers(userIdsByEmail));
		}

		await context.SaveChangesAsync();

		await BackfillBulkUploadsModuleAsync(context, initData);
	}

	// The seed blocks above only run on an empty table, so a module added after the
	// first deployment would never reach an existing database. This backfills the Bulk
	// Uploads module and grants it to everyone who can already reach New Order, which
	// is the access rule the module follows. Idempotent: a second run adds nothing.
	private static async Task BackfillBulkUploadsModuleAsync(
		ATSDBContext context,
		ATSInitialData initData)
	{
		var moduleExists = await context.ModuleDetails
			.AnyAsync(module => module.ModuleId == AtsModuleIds.BulkUploads);

		if (!moduleExists)
		{
			var bulkUploadsModule = initData.GetATSModules()
				.FirstOrDefault(module => module.ModuleId == AtsModuleIds.BulkUploads);

			if (bulkUploadsModule is null)
			{
				return;
			}

			await context.ModuleDetails.AddAsync(bulkUploadsModule);
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
			.Where(user => user.ModuleId == AtsModuleIds.BulkUploads)
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
				ModuleId = AtsModuleIds.BulkUploads,
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
