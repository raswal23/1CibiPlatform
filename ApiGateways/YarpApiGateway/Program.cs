using ApiGateways.YarpApiGateway.Extensions;
using ApiGateways.YarpApiGateway.Services;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// central registration for gateway concerns
builder.AddGatewayServices();

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

app.MapReverseProxy();

app.Run();
