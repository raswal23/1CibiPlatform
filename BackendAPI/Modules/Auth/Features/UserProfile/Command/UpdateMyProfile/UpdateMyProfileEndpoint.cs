namespace Auth.Features.UserProfile.Command.UpdateMyProfile;

public record UpdateMyProfileRequest(UpdateUserProfileDTO updateProfile);

public record UpdateMyProfileResponse(UserProfileDTO Profile);

public class UpdateMyProfileEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapPatch("auth/updatemyprofile", async (
			UpdateMyProfileRequest request,
			ISender sender,
			CancellationToken cancellationToken) =>
		{
			var command = new UpdateMyProfileCommand(request.updateProfile);

			UpdateMyProfileResult result = await sender.Send(command, cancellationToken);

			var response = new UpdateMyProfileResponse(result.Profile);

			return Results.Ok(response);
		})
		.WithName("UpdateMyProfile")
		.WithTags("User Profile")
		.Produces<UpdateMyProfileResponse>(StatusCodes.Status200OK)
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status401Unauthorized)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.WithSummary("Update my profile")
		.WithDescription("Updates the authenticated user's own first, middle, and last name.")
		.RequireAuthorization();
	}
}
