using AIAgent.Hubs;
using ATS.Hubs;

namespace APIs.ServiceConfig;

public static class AppConfiguration
{
	#region Runtime Configuration
	public static async Task<WebApplication> UseEnvironmentAsync(this WebApplication app)
	{
		app.UseForwardedHeaders();

		if (app.Environment.IsDevelopment())
		{
			await DatabaseExtensions.IntializeDatabaseAsync(app);
			app.UseSwagger();
			app.UseSwaggerUI();
		}

		if (app.Environment.IsEnvironment("Testing"))
		{
			return app;
		}

		if (app.Environment.IsEnvironment("Sandbox"))
		{
			await DatabaseExtensions.IntializeDatabaseAsync(app);
		}

		if (app.Environment.IsEnvironment("UAT"))
		{
			await DatabaseExtensions.IntializeDatabaseAsync(app);
		}

		if (app.Environment.IsProduction())
		{
			// Swagger stays off here. It publishes a full map of every endpoint, DTO and
			// parameter - including the anonymous application-form routes - to anyone who
			// can reach the host. Sandbox and UAT already omit it; Production had
			// inherited the Development branch by copy.
			await DatabaseExtensions.IntializeDatabaseAsync(app);
		}

		return app;
	}


	#endregion

	#region Custom Middlewares
	public static WebApplication UseCustomMiddlewares(this WebApplication app)
	{
		app.UseHttpsRedirection();
		app.UseRouting();

		// Sits between UseRouting and UseExceptionHandler, and both sides matter.
		//
		// After UseRouting: the endpoint is resolved, so requests are labelled with the
		// route template rather than the raw path. Labelling by raw path would create a
		// new time series per unique URL and exhaust Prometheus memory.
		//
		// Before UseExceptionHandler: prometheus-net records the status code as it
		// unwinds, so any middleware that rewrites the code must run *inside* it.
		// Registered the other way round, an unhandled exception is reported to the
		// client as 500 but counted as 200 - which would silently disable the
		// HighErrorRate alert, the one rule meant to catch exactly this.
		app.UseHttpMetrics();

		// Consequence of the ordering above: exceptions thrown by UseHttpsRedirection or
		// UseRouting themselves are no longer caught here. Both are effectively incapable
		// of throwing for a well-formed request, which is a cheap price for correct
		// error-rate metrics.
		app.UseExceptionHandler(options => { });

		app.UseCors("CorsPolicy");
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapControllers();
		app.MapCarter();

		app.MapObservabilityEndpoints();

		return app;
	}
	#endregion

	#region Observability Endpoints
	/// <summary>
	/// Maps <c>/metrics</c> and <c>/health</c>. Both are intentionally left anonymous:
	/// this host is only reachable on the internal Docker network (the gateway proxies
	/// to <c>apis:8080</c>), so the network boundary - not authentication - is what keeps
	/// them private. The gateway blocks both paths from the public internet; see
	/// <c>docs/monitoring/03-app-instrumentation.md</c>.
	/// </summary>
	public static WebApplication MapObservabilityEndpoints(this WebApplication app)
	{
		// Liveness: the process is up and the pipeline responds. Deliberately runs no
		// checks, so a database outage does not make an orchestrator kill a healthy app.
		app.MapHealthChecks("/health/live", new HealthCheckOptions
		{
			Predicate = _ => false
		}).AllowAnonymous();

		// Readiness: dependencies are reachable. This is what /health aliases and what
		// the monitoring server treats as "can this instance serve traffic".
		app.MapHealthChecks("/health/ready", new HealthCheckOptions
		{
			Predicate = check => check.Tags.Contains("ready"),
			ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
		}).AllowAnonymous();

		app.MapHealthChecks("/health", new HealthCheckOptions
		{
			ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
		}).AllowAnonymous();

		app.MapMetrics().AllowAnonymous();

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
