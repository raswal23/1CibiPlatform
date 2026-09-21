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

		// The sender the queue used before accounts became rows. Seeded so the migration is
		// deployable on its own: an empty table means the selector finds nothing sendable and
		// every invitation defers until somebody registers an account by hand.
		//
		// Guarded on emptiness rather than on the address, so an operator who deliberately
		// deletes this account does not get it back on the next restart.
		if (!await context.EmailAccounts.AsNoTracking().AnyAsync())
		{
			var primaryAccount = initData.GetPrimaryEmailAccount();

			if (primaryAccount is not null)
			{
				await context.EmailAccounts.AddAsync(primaryAccount);
			}
		}

		await context.SaveChangesAsync();

		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.BulkUploads);
		await BackfillModuleGrantedWithNewOrderAsync(context, initData, AtsModuleIds.TicketingStatus);
		await BackfillRoleAsync(context, initData, AtsRoleIds.ClientExperience);

		// Last, because the backfills above insert RoleDetails and ModuleDetails rows with
		// explicit ids too. Syncing before them would leave the sequence stranded again.
		await SyncIdentitySequencesAsync(context);
	}

	/// <summary>
	/// Advances the identity sequences of the tables this seed fills with explicit ids,
	/// so the next admin-created row does not collide with a seeded one.
	/// </summary>
	/// <remarks>
	/// RoleDetails and ModuleDetails are seeded with explicit ids (RoleId 1-4, the
	/// AtsModuleIds constants), and an explicit id does not advance the identity
	/// sequence behind the column. The sequence therefore still points at 1, so the
	/// first role or module added through the admin screens is handed an id that is
	/// already taken and the insert dies on the primary key - which the UI reports as
	/// the generic "error saving entity". Mirrors AuthDatabaseExtensions, which hit
	/// this first. Runs unconditionally, not only when the seed inserted something:
	/// databases seeded before this existed are already wrong and heal on next start.
	/// Public so the integration tests can reproduce a seeded database, which they
	/// otherwise never see - they truncate with RESTART IDENTITY.
	/// </remarks>
	public static async Task SyncIdentitySequencesAsync(ATSDBContext context)
	{
		// GREATEST guards an empty table, where MAX is NULL and setval would fail.
		// TRUE marks the value as used, so the next id is MAX + 1.
		await context.Database.ExecuteSqlRawAsync(
			"""
			SELECT setval(
				pg_get_serial_sequence('ats."RoleDetails"', 'RoleId'),
				GREATEST((SELECT MAX("RoleId") FROM ats."RoleDetails"), 1),
				TRUE);

			SELECT setval(
				pg_get_serial_sequence('ats."ModuleDetails"', 'ModuleId'),
				GREATEST((SELECT MAX("ModuleId") FROM ats."ModuleDetails"), 1),
				TRUE);
			""");
	}

	/// <summary>
	/// Inserts one seeded role into a database whose RoleDetails table is already populated.
	/// </summary>
	/// <remarks>
	/// The role seed above is guarded on an empty table, so a role added after the first
	/// deployment reaches new databases only - every existing environment would be missing
	/// it, and any user assigned to it would fail the FK on UserDetails. Matched on RoleId
	/// rather than name so an operator who renamed the row does not get a duplicate, and
	/// idempotent for the same reason: a second run adds nothing.
	/// </remarks>
	private static async Task BackfillRoleAsync(
		ATSDBContext context,
		ATSInitialData initData,
		int roleId)
	{
		if (await context.RoleDetails.AnyAsync(role => role.RoleId == roleId))
		{
			return;
		}

		var seededRole = initData.GetATSRoles()
			.FirstOrDefault(candidate => candidate.RoleId == roleId);

		if (seededRole is null)
		{
			return;
		}

		await context.RoleDetails.AddAsync(seededRole);
		await context.SaveChangesAsync();
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
