namespace Auth.Features.UserProfile.Query.GetMyProfile;

public record GetMyProfileEndpointResponse(UserProfileDTO Profile);

public class GetMyProfileEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("auth/getmyprofile", async (
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var query = new GetMyProfileQueryRequest();

			var result = await sender.Send(query, cancellationToken);

			var response = new GetMyProfileEndpointResponse(result.Profile);

			return Results.Ok(response);
		})
		.WithName("GetMyProfile")
		.WithTags("User Profile")
		.Produces<GetMyProfileEndpointResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Get my profile")
		.WithDescription("Retrieves the authenticated user's own profile details.")
		.RequireAuthorization();
	}
}
