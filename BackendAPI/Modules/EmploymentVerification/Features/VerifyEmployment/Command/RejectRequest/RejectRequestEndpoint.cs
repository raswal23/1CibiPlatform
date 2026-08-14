namespace EmploymentVerification.Features.VerifyEmployment.Command.RejectRequest;

public sealed class RejectRequestEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPost(
				"api/employment-verification/reject/{token}",
				async (
					string token,
					ISender sender,
					CancellationToken cancellationToken) =>
				{
					var result = await sender.Send(
						new RejectRequestCommand(token),
						cancellationToken);

					return result.Status switch
					{
						CompletionStatus.Completed =>
							Results.Ok(result.Request),

						CompletionStatus.AlreadyCompleted =>
							Results.Problem(
								title: "TokenAlreadyUsed",
								detail: "This verification link has already been answered. Your earlier response was kept.",
								statusCode: StatusCodes.Status409Conflict),

						CompletionStatus.Expired =>
							Results.Problem(
								title: "TokenExpired",
								detail: "This verification link has expired. Please ask CIBI to send a new request.",
								statusCode: StatusCodes.Status410Gone),

						_ =>
							Results.Problem(
								title: "TokenNotFound",
								detail: "This verification link is not valid.",
								statusCode: StatusCodes.Status404NotFound)
					};
				})
			// The HR recipient answers from email and has no platform account.
			.AllowAnonymous()
			.WithName("RejectEmploymentVerificationRequest")
			.WithTags("Employment Verification")
			.Produces<EmploymentVerificationPreviewDTO>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status410Gone)
			.WithSummary("Report the employment details behind an emailed token as inaccurate")
			.WithDescription(
				"Marks the request Rejected and stamps RejectedAt. The link is single use, "
				+ "so a repeat call reports that the request was already answered.");
	}
}
