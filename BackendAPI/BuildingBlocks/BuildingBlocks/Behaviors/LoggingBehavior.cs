using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BuildingBlocks.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull, IRequest<TResponse>
	where TResponse : notnull
{
	private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
	private readonly IHttpContextAccessor _httpContextAccessor;

	public LoggingBehavior(
		ILogger<LoggingBehavior<TRequest, TResponse>> logger,
		IHttpContextAccessor httpContextAccessor)
	{
		_logger = logger;
		_httpContextAccessor = httpContextAccessor;
	}

	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		var httpContext = _httpContextAccessor.HttpContext;
		var userId = httpContext?.User?.FindFirst("userId")?.Value ?? "anonymous";
		var email = httpContext?.User?.FindFirst(ClaimTypes.Email)?.Value ?? "unknown@example.com";
		var fullName = httpContext?.User?.FindFirst("fullName")?.Value ?? "unknown";
		var application = GetApplicationName(typeof(TRequest));

		using (_logger.BeginScope(new Dictionary<string, object>
		{
			["Platform"] = "1CibiPlatform",
			["Application"] = application,
			["UserId"] = userId,
			["Email"] = email,
			["FullName"] = fullName
		}))
		{
			_logger.LogInformation("[START] Handling {Request} - Request Data: {@Request}", typeof(TRequest).Name, request);

			var stopwatch = Stopwatch.StartNew();
			var response = await next();
			stopwatch.Stop();

			if (stopwatch.Elapsed.TotalSeconds > 3)
			{
				_logger.LogWarning("[PERFORMANCE] {Request} took {ElapsedSeconds} seconds", typeof(TRequest).Name, stopwatch.Elapsed.TotalSeconds);
			}

			_logger.LogInformation("[END] Handled {Request} - Response Data: {@Response}", typeof(TRequest).Name, response);

			return response;
		}
	}

	private static string GetApplicationName(Type requestType)
	{
		var rootNamespace = requestType.Namespace?
			.Split('.', StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault();

		return rootNamespace switch
		{
			"ATS" => "ATS",
			"Auth" => "Auth",
			"PhilSys" => "PhilSys",
			"AIAgent" => "AIAgent",
			"CNX" => "CNX",
			"SSO" => "SSO",
			"PlatformLogging" => "PlatformLogging",
			_ => "Unknown"
		};
	}
}

