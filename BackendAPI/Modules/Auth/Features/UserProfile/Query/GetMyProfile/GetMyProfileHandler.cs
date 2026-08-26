namespace Auth.Features.UserProfile.Query.GetMyProfile;

public record GetMyProfileQueryRequest() : IQuery<GetMyProfileQueryResult>;

public record GetMyProfileQueryResult(UserProfileDTO Profile);

public class GetMyProfileQueryHandler : IQueryHandler<GetMyProfileQueryRequest, GetMyProfileQueryResult>
{
	private readonly IUserProfileService _userProfileService;

	public GetMyProfileQueryHandler(IUserProfileService userProfileService)
	{
		_userProfileService = userProfileService;
	}

	public async Task<GetMyProfileQueryResult> Handle(
		GetMyProfileQueryRequest request,
		CancellationToken cancellationToken)
	{
		var profile = await _userProfileService.GetMyProfileAsync(cancellationToken);

		return new GetMyProfileQueryResult(profile);
	}
}
