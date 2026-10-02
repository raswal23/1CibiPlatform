namespace ATS.Features.Web.EmailAccountManagement.Query.GetEmailAccounts;

public record GetEmailAccountsResponse(List<EmailAccountDTO> emailAccounts);

public class GetEmailAccountsEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("getemailaccounts", async (ISender sender, CancellationToken cancellationToken) =>
		{
			var result = await sender.Send(new GetEmailAccountsQuery(), cancellationToken);
			var response = new GetEmailAccountsResponse(result.emailAccounts);

			return Results.Ok(response);
		})
		.WithName("GetEmailAccounts")
		.WithTags("ATS")
		.Produces<GetEmailAccountsResponse>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.WithSummary("Get Email Accounts")
		.WithDescription("Every registered sender account with its health and rolling-window consumption.")
		.RequireAuthorization();
	}
}
