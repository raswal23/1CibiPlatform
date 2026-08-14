namespace EmploymentVerification.Features.VerifyEmployment.Query.GetRequestByToken;

public sealed class GetRequestByTokenEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet(
				"api/employment-verification/preview/{token}",
				async (
					string token,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var result = await sender.Send(
						new GetRequestByTokenQuery(token),
						cancellationToken);

					return result.Status switch
					{
						PreviewTokenStatus.Valid =>
							Results.Ok(result.Request),

						PreviewTokenStatus.Expired =>
							Results.Problem(
								title: "TokenExpired",
								detail: "This verification link has expired. Please ask CIBI to send a new request.",
								statusCode: StatusCodes.Status410Gone),

						PreviewTokenStatus.AlreadyCompleted =>
							Results.Problem(
								title: "TokenAlreadyUsed",
								detail: "This verification link has already been used and cannot be opened again.",
								statusCode: StatusCodes.Status409Conflict),

						_ =>
							Results.Problem(
								title: "TokenNotFound",
								detail: "This verification link is not valid.",
								statusCode: StatusCodes.Status404NotFound)
					};
				})
			// The HR recipient opens this link from email and has no platform account.
			.AllowAnonymous()
			.WithName("GetEmploymentVerificationPreview")
			.WithTags("Employment Verification")
			.Produces<EmploymentVerificationPreviewDTO>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status410Gone)
			.WithSummary("Preview an employment verification request by emailed token")
			.WithDescription(
				"Validates the single-use token from the verification email and returns the "
				+ "request details when the token is unexpired and unused. Returns a problem "
				+ "response describing why the link cannot be opened otherwise.");
	}
}
