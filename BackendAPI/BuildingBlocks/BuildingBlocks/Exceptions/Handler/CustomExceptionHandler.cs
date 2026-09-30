using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Exceptions.Handler;
public class CustomExceptionHandler
	(ILogger<CustomExceptionHandler> logger)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
	{
		logger.LogError(
			"Error Message: {exceptionMessage}, Time of occurrence {time}",
			exception.Message, DateTime.UtcNow);

		(string Detail, string Title, int StatusCode) details = exception switch
		{
			InternalServerException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status500InternalServerError
			),
			ValidationException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status400BadRequest
			),
			BadRequestException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status400BadRequest
			),
			NotFoundException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status404NotFound
			),
			UnauthorizedException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status401Unauthorized
			),
			// System.UnauthorizedAccessException is not the BuildingBlocks type above and
			// was previously falling through to the 500 default. The auth module throws it
			// for genuine authentication failures - a rejected refresh token, a locked
			// account, an unapproved account - all of which the client must be able to
			// tell apart from a server fault.
			UnauthorizedAccessException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status401Unauthorized
			),
			ForbiddenException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status403Forbidden
			),
			ConflictException =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status409Conflict
			),
			_ =>
			(
				exception.Message,
				exception.GetType().Name,
				context.Response.StatusCode = StatusCodes.Status500InternalServerError
			)
		};

		var problemDetails = new ProblemDetails
		{
			Title = details.Title,
			Detail = details.Detail,
			Status = details.StatusCode,
			Instance = context.Request.Path
		};

		problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

		if (exception is ValidationException validationException)
		{
			var errors = validationException.Errors.Select(er => er.ErrorMessage.Replace(".", ""));

			problemDetails.Extensions.Add("ValidationErrors", errors);
			problemDetails.Detail = string.Join(", ", errors);
		}

		await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);
		return true;
	}
}
