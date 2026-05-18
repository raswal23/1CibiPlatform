var builder = WebApplication.CreateBuilder(args);
var assembly = typeof(Program).Assembly;

builder.Services.ConfigureEnvironment(builder);

builder.Services.AddControllers();

builder.Services
	.AddLoggingConfiguration(builder.Configuration)
	.AddModuleMediaTR()
	.AddModuleCarter()
	.AddHybridCaches(builder.Configuration)
	.AddModuleServices()
	.AddJwtAuthentication(builder.Configuration, builder.Environment)
	.AddModuleInfrastructure(builder.Configuration)
	.AddAIAgentConfiguration(builder.Configuration)
	.AddAIAgentSkills(builder.Configuration)
	.AddEndpointsApiExplorer()
	.AddSwaggerGen();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownNetworks.Clear();
	options.KnownProxies.Clear();
});

var app = builder.Build();

await app.UseEnvironmentAsync();
app.UseAIAgentSkills();
app.UseSignalRConfiguration(builder.Configuration);
app.UseCustomMiddlewares();
app.Run();

public partial class Program { }