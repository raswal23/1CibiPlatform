namespace ATS.Features.UserManagement.Query.GetUserClientAssignments;

public record GetUserClientAssignmentsQuery : IQuery<GetUserClientAssignmentsResult>;

public record GetUserClientAssignmentsResult(IReadOnlyList<UserClientDetailsDTO> assignments);

public class GetUserClientAssignmentsHandler
	: IQueryHandler<GetUserClientAssignmentsQuery, GetUserClientAssignmentsResult>
{
	private readonly IUserManagementService _userManagementService;

	public GetUserClientAssignmentsHandler(IUserManagementService userManagementService)
	{
		_userManagementService = userManagementService;
	}

	public async Task<GetUserClientAssignmentsResult> Handle(
		GetUserClientAssignmentsQuery request,
		CancellationToken cancellationToken)
	{
		var assignments = await _userManagementService.GetUserClientAssignmentsAsync(cancellationToken);
		return new GetUserClientAssignmentsResult(assignments);
	}
}
