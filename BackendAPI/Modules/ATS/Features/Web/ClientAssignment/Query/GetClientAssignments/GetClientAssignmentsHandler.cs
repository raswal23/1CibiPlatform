namespace ATS.Features.Web.ClientAssignment.Query.GetClientAssignments;

public record GetClientAssignmentsQuery(KeysetPaginationRequest KeysetPaginationRequest)
	: IQuery<GetClientAssignmentsResult>;

public record GetClientAssignmentsResult(
	KeysetPaginatedResult<ClientAssignmentDetailsDTO> Assignments);

public sealed class GetClientAssignmentsQueryValidator
	: AbstractValidator<GetClientAssignmentsQuery>
{
	public GetClientAssignmentsQueryValidator()
	{
		RuleFor(query => query.KeysetPaginationRequest.PageSize)
			.InclusiveBetween(1, 100);
		RuleFor(query => query.KeysetPaginationRequest.SearchTerm)
			.MaximumLength(200);
	}
}

public sealed class GetClientAssignmentsHandler
	: IQueryHandler<GetClientAssignmentsQuery, GetClientAssignmentsResult>
{
	private readonly IClientAssignmentService _clientAssignmentService;

	public GetClientAssignmentsHandler(IClientAssignmentService clientAssignmentService)
	{
		_clientAssignmentService = clientAssignmentService;
	}

	public async Task<GetClientAssignmentsResult> Handle(
		GetClientAssignmentsQuery request,
		CancellationToken cancellationToken)
	{
		var assignments = await _clientAssignmentService.GetAssignmentsAsync(
			request.KeysetPaginationRequest,
			cancellationToken);
		return new GetClientAssignmentsResult(assignments);
	}
}
