namespace ATS.Features.ClientAssignment.Query.GetClientAssignments;

public record GetClientAssignmentsQuery(PaginationRequest PaginationRequest)
	: IQuery<GetClientAssignmentsResult>;

public record GetClientAssignmentsResult(
	PaginatedResult<ClientAssignmentDetailsDTO> Assignments);

public sealed class GetClientAssignmentsQueryValidator
	: AbstractValidator<GetClientAssignmentsQuery>
{
	public GetClientAssignmentsQueryValidator()
	{
		RuleFor(query => query.PaginationRequest.PageIndex)
			.GreaterThan(0);
		RuleFor(query => query.PaginationRequest.PageSize)
			.InclusiveBetween(1, 100);
		RuleFor(query => query.PaginationRequest.SearchTerm)
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
			request.PaginationRequest,
			cancellationToken);
		return new GetClientAssignmentsResult(assignments);
	}
}
