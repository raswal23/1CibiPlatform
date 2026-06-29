using AIAgent.Hubs;
using ATS.Hubs;

namespace APIs.ServiceConfig;

public static class AppConfiguration
{
	#region Runtime Configuration
	public static async Task<WebApplication> UseEnvironmentAsync(this WebApplication app)
	{
		app.UseForwardedHeaders();

		if (app.Environment.IsEnvironment("Testing"))
		{
			return app;
		}

		if (app.Environment.IsEnvironment("UAT"))
		{
			await DatabaseExtensions.IntializeDatabaseAsync(app);
		}

		if (app.Environment.IsDevelopment())
		{
			await DatabaseExtensions.IntializeDatabaseAsync(app);
			app.UseSwagger();
			app.UseSwaggerUI();

		}

		if (app.Environment.IsProduction())
		{
			await DatabaseExtensions.IntializeDatabaseAsync(app);
			app.UseSwagger();
			app.UseSwaggerUI();
		}

		return app;
	}


	#endregion

	#region Custom Middlewares
	public static WebApplication UseCustomMiddlewares(this WebApplication app)
	{
		app.UseExceptionHandler(options => { });
		app.UseHttpsRedirection();
		app.UseRouting();
		app.UseCors("CorsPolicy");
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapControllers();
		app.MapCarter();

		return app;
	}
	#endregion

	#region SignalR Configuration	
	public static WebApplication UseSignalRConfiguration(
		this WebApplication app,
		IConfiguration configuration)
	{
		if (app.Environment.IsEnvironment("Testing"))
		{
			return app;
		}

		app.MapHub<AIAgentHub>(configuration["SignalRHub:Endpoint"]!);
		app.MapHub<ATSHub>(configuration["SignalRHub:ATSBulkEndpoint"]!);
		app.UseWebSockets();
		return app;
	} 
	#endregion

	#region AI agent app skills configuration
	public static WebApplication UseAIAgentSkillsConfiguration(this WebApplication app)
	{
		app.UseAIAgentSkills();
		return app;
	}
	#endregion
}
