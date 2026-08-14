namespace EmploymentVerification.Data.Extensions;

public static class EmploymentVerificationDatabaseExtensions
{
	public static async Task EmploymentVerificationInitializeDatabaseAsync(
		this WebApplication app)
	{
		await using var scope = app.Services.CreateAsyncScope();
		var context = scope.ServiceProvider
			.GetRequiredService<EmploymentVerificationDbContext>();

		await context.Database.MigrateAsync();
	}
}
