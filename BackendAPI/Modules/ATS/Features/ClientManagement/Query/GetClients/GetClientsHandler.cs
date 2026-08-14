namespace ATS.Features.ClientManagement.Query.GetClients;

public record GetClientsQuery(PaginationRequest paginationRequest) : IQuery<GetClientsResult>;

public class GetClientsQueryValidator : AbstractValidator<GetClientsQuery>
{
	public GetClientsQueryValidator()
	{
		RuleFor(x => x.paginationRequest)
			.NotNull().WithMessage("Pagination request is required.");
	}
}

public record GetClientsResult(PaginatedResult<ClientDetailsDTO> clients);

public class GetClientsHandler : IQueryHandler<GetClientsQuery, GetClientsResult>
{
	private readonly IClientManagementService _clientManagementService;

	public GetClientsHandler(IClientManagementService clientManagementService)
	{
		_clientManagementService = clientManagementService;
	}

	public async Task<GetClientsResult> Handle(GetClientsQuery request, CancellationToken cancellationToken)
	{
		var clients = await _clientManagementService.GetClientsAsync(request.paginationRequest, cancellationToken);
		return new GetClientsResult(clients);
	}
}
