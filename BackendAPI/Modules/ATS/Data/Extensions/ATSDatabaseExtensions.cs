namespace ATS.Data.Extensions;

public static class ATSDatabaseExtensions
{
    public static async Task ATSIntializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ATSDBContext>();
        await context.Database.MigrateAsync();
    }
}
