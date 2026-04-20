
namespace APIs.Data.Extensions;

public static class DatabaseExtensions
{
	public static async Task IntializeDatabaseAsync(this WebApplication app)
	{
		AuthDatabaseExtensions.AuthIntializeDatabaseAsync(app).GetAwaiter().GetResult();
		//CNXDatabaseExtensions.CNXIntializeDatabaseAsync(app).GetAwaiter().GetResult();
		PhilSysDatabaseExtensions.PhilSysIntializeDatabaseAsync(app).GetAwaiter().GetResult();
		AIAgentDatabaseExtensions.AIAgentIntializeDatabaseAsync(app).GetAwaiter().GetResult();
		ATSDatabaseExtensions.ATSIntializeDatabaseAsync(app).GetAwaiter().GetResult();

	}
}
