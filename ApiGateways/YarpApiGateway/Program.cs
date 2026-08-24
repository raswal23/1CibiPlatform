using ApiGateways.YarpApiGateway.Extensions;
using ApiGateways.YarpApiGateway.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Prometheus;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// central registration for gateway concerns
builder.AddGatewayServices();

// The gateway proxies rather than owning state, so its meaningful health signal is
// "can I still reach what I proxy to".
//
// The named check is not decorative. ForwardToPrometheus() installs an
// IHealthCheckPublisher, and the hosted service that drives publishers short-circuits
// when NO checks are registered - so `AddHealthChecks().ForwardToPrometheus()` on its
// own publishes nothing at all, and aspnetcore_healthcheck_status never appears for
// this process. Registering at least one check is what makes the gauge exist.
builder.Services
	.AddHealthChecks()
	.AddCheck("gateway", () => HealthCheckResult.Healthy("Gateway is routing requests"),
		tags: ["live"])
	.ForwardToPrometheus();

// Kestrel configuration: keep production PFX loading for certificates
builder.WebHost.ConfigureKestrel(kestrel =>
{
	if (builder.Environment.IsDevelopment())
	{
		Console.WriteLine("🔧 Development mode — using ASP.NET Core dev certificate.");
		kestrel.ConfigureHttpsDefaults(https =>
		{
			// This ensures dev cert is used for any HTTPS endpoint
		});
	}
	else
	{
		// 🐳 PRODUCTION (Docker/Server): Load from PFX
		kestrel.ListenAnyIP(443, opts =>
		{
			var certPath = "/app/certs/mycert.pfx";
			var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD")
				?? throw new InvalidOperationException("CERT_PASSWORD is not set in production.");

			var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
			var daysUntilExpiry = (cert.NotAfter - DateTime.UtcNow).TotalDays;

			if (daysUntilExpiry < 30)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"⚠️ WARNING: Certificate expires in {daysUntilExpiry:F0} days!");
				Console.ResetColor();
			}

			Console.WriteLine($"✅ Production cert loaded: {cert.Subject} — Expires: {cert.NotAfter:yyyy-MM-dd HH:mm}");
			opts.UseHttps(cert);
		});
	}
});

var app = builder.Build();

app.UseRouting();

// After UseRouting so the route template - not the raw path - becomes the metric label.
// Labelling by raw path would create a new time series per unique URL and exhaust
// Prometheus memory on the first crawler that walks the site.
app.UseHttpMetrics();

// The gateway is the only public entry point, so /metrics and /health must be closed
// off here or they are exposed to the internet. /metrics in particular enumerates every
// route name and error count - the same class of disclosure that keeps Swagger disabled
// in Production. Only the monitoring server (and the Docker network) may reach them.
//
// Read once at startup rather than per request: the allowlist changes only on restart,
// and re-binding the array on every scrape is pure waste.
var allowedScrapeNetworks = app.Configuration
	.GetSection("Monitoring:AllowedScrapeNetworks")
	.Get<string[]>() ?? [];

// An empty allowlist is the correct default - an unconfigured host must expose nothing -
// but it also means every scrape 404s, which reads as a broken exporter rather than
// missing configuration. Say so once at startup so the cause is in the logs before
// anyone starts debugging Prometheus.
if (allowedScrapeNetworks.Length == 0 && !app.Environment.IsDevelopment())
{
	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine(
		"⚠️  Monitoring:AllowedScrapeNetworks is empty - /metrics and /health will " +
		"return 404 to every caller. Set it to the monitoring server's private " +
		"address (e.g. MONITORING__ALLOWEDSCRAPENETWORKS__0=10.0.0.50/32) or the " +
		"Prometheus target for this gateway will stay DOWN.");
	Console.ResetColor();
}

app.Use(async (context, next) =>
{
	var path = context.Request.Path;

	if (path.StartsWithSegments("/metrics") || path.StartsWithSegments("/health"))
	{
		if (!MonitoringAccess.IsAllowed(
			context.Connection.RemoteIpAddress,
			allowedScrapeNetworks,
			app.Environment))
		{
			context.Response.StatusCode = StatusCodes.Status404NotFound;
			return;
		}
	}

	await next();
});

// Security response headers (CSP, X-Frame-Options, HSTS, etc.)
app.Use(async (context, next) =>
{
	context.Response.Headers["Content-Security-Policy"] =
		"default-src 'self'; " +
		"script-src 'self' 'wasm-unsafe-eval' 'unsafe-inline' https://s.go-mpulse.net; " +
		"style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
		"img-src 'self' data: blob: https://ekycbucket.s3.ap-southeast-1.amazonaws.com https://face-liveness-ws.s3.ap-northeast-1.amazonaws.com; " +
		"font-src 'self' https://fonts.gstatic.com; " +
		"frame-src 'self' https://liveness.everify.gov.ph; " +   
		"media-src 'self' https://liveness.everify.gov.ph; " +
		"connect-src 'self' https: wss: https://s.go-mpulse.net; " +
		"object-src 'none'; " +
		"frame-ancestors 'self';";

	context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
	context.Response.Headers["X-Content-Type-Options"] = "nosniff";
	context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
	context.Response.Headers["Permissions-Policy"] =
		"camera=(self \"https://liveness.everify.gov.ph\"), microphone=(), geolocation=(), payment=()";

	// HSTS only over HTTPS and never in local dev (RFC 6797; avoids poisoning the browser HSTS cache for localhost)
	if (!app.Environment.IsDevelopment() && context.Request.IsHttps)
	{
		context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
	}

	await next();
});

// enable middleware
app.UseRateLimiter();
app.UseWebSockets();
app.UseCors("CorsPolicy");

// diagnostic endpoint to inspect discovered routes/clusters
app.MapGet("/__routes", (RouteCatalog catalog) => Results.Ok(new { routes = catalog.Routes, clusters = catalog.Clusters }));

// All of these must be mapped before MapReverseProxy: the "FrontEndEntryPoint" route
// matches "/{**catchall}", so anything registered afterwards is swallowed and proxied
// to the Blazor frontend instead of being served here.
app.MapHealthChecks("/health", new HealthCheckOptions
{
	ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// /health/live and /health/ready exist on the backend, and the IP gate above covers
// the whole "/health" segment - so without mapping them here they fall through to the
// catch-all and get proxied to the frontend, which answers 502 (or worse, 200 with the
// SPA shell once the frontend container is up). Mapping them keeps every observability
// path terminating at the gateway.
//
// The gateway proxies rather than owning state, so liveness and readiness are the same
// question for it: is this process routing requests.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
	Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapMetrics();

app.MapReverseProxy();

app.Run();
