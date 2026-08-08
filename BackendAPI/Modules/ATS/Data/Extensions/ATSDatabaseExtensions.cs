namespace ATS.Data.Extensions;

public static class ATSDatabaseExtensions
{
	public static async Task ATSIntializeDatabaseAsync(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();

		var context = scope.ServiceProvider.GetRequiredService<ATSDBContext>();
		var initData = scope.ServiceProvider.GetRequiredService<ATSInitialData>();

		await context.Database.MigrateAsync();
		await SeedAsync(context, initData);

		await InitializeQuartzAsync(context);
	}

	private static async Task SeedAsync(
		ATSDBContext context,
		ATSInitialData initData)
	{
		if (await context.EmailInvitationRequests
			.AsNoTracking()
			.AnyAsync())
		{
			return;
		}

		await context.EmailInvitationRequests.AddRangeAsync(
			initData.GetEmailInvitationRequests());
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
