namespace ATS.Shared;

public static class AtsEndpointExtensions
{
	/// <summary>
	/// Rejects authenticated callers whose ATS account is disabled (no active
	/// <c>UserDetails</c> row with an active role) with a 403 ProblemDetails.
	/// Platform super admins bypass. Apply after <c>RequireAuthorization</c> on
	/// every internal ATS web endpoint; never on the anonymous candidate flows
	/// or the public API, whose callers are not ATS console users.
	/// </summary>
	public static RouteHandlerBuilder RequireActiveAtsUser(this RouteHandlerBuilder builder) =>
		builder
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.AddEndpointFilter(async (context, next) =>
			{
				var guard = context.HttpContext.RequestServices.GetRequiredService<IAtsActiveUserGuard>();
				await guard.EnsureActiveAtsUserAsync(context.HttpContext.RequestAborted);
				return await next(context);
			});
}
