namespace ATS.Features.ClientAssignment.Query.GetAssignableClients;

public record GetAssignableClientsQuery(PaginationRequest PaginationRequest)
	: IQuery<GetAssignableClientsResult>;

public record GetAssignableClientsResult(PaginatedResult<ClientLookupDTO> Clients);

public sealed class GetAssignableClientsQueryValidator
	: AbstractValidator<GetAssignableClientsQuery>
{
	public GetAssignableClientsQueryValidator()
	{
		RuleFor(query => query.PaginationRequest.PageIndex)
			.GreaterThan(0);
		RuleFor(query => query.PaginationRequest.PageSize)
			.InclusiveBetween(1, 100);
		RuleFor(query => query.PaginationRequest.SearchTerm)
			.MaximumLength(200);
	}
}

public sealed class GetAssignableClientsHandler
	: IQueryHandler<GetAssignableClientsQuery, GetAssignableClientsResult>
{
	private readonly IClientAssignmentService _clientAssignmentService;

	public GetAssignableClientsHandler(IClientAssignmentService clientAssignmentService)
	{
		_clientAssignmentService = clientAssignmentService;
	}

	public async Task<GetAssignableClientsResult> Handle(
		GetAssignableClientsQuery request,
		CancellationToken cancellationToken)
	{
		var clients = await _clientAssignmentService.GetAssignableClientsAsync(
			request.PaginationRequest,
			cancellationToken);
		return new GetAssignableClientsResult(clients);
	}
}
