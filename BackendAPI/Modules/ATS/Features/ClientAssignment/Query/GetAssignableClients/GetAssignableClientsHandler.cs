namespace ATS.Features.ClientAssignment.Query.GetAssignableClients;

public record GetAssignableClientsQuery(KeysetPaginationRequest KeysetPaginationRequest)
	: IQuery<GetAssignableClientsResult>;

public record GetAssignableClientsResult(KeysetPaginatedResult<ClientLookupDTO> Clients);

public sealed class GetAssignableClientsQueryValidator
	: AbstractValidator<GetAssignableClientsQuery>
{
	public GetAssignableClientsQueryValidator()
	{
		RuleFor(query => query.KeysetPaginationRequest.PageSize)
			.InclusiveBetween(1, 100);
		RuleFor(query => query.KeysetPaginationRequest.SearchTerm)
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
			request.KeysetPaginationRequest,
			cancellationToken);
		return new GetAssignableClientsResult(clients);
	}
}
