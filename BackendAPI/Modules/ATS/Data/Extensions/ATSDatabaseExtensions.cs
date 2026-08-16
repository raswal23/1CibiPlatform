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

		if (!await context.UserDetails.AnyAsync())
		{
			var userIdsByEmail = await authQueries.GetUserIdsByEmailAsync(
				ATSInitialData.GetATSUserEmails().ToArray(),
				CancellationToken.None);

			await context.UserDetails.AddRangeAsync(
				initData.GetATSUsers(userIdsByEmail));
		}

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
