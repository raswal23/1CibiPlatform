namespace PlatformLogging.Data.Extensions;

public static class PlatformLoggingDatabaseExtensions
{
	public static async Task PlatformLoggingInitializeDatabaseAsync(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<PlatformLoggingDBContext>();
		await context.Database.MigrateAsync();
	}
}
