namespace ATS.Features.Web.ClientManagement.Query.GetClients;

public record GetClientsQuery(KeysetPaginationRequest KeysetPaginationRequest) : IQuery<GetClientsResult>;

public class GetClientsQueryValidator : AbstractValidator<GetClientsQuery>
{
	public GetClientsQueryValidator()
	{
		RuleFor(x => x.KeysetPaginationRequest)
			.NotNull().WithMessage("Pagination request is required.");
	}
}

public record GetClientsResult(KeysetPaginatedResult<ClientDetailsDTO> clients);

public class GetClientsHandler : IQueryHandler<GetClientsQuery, GetClientsResult>
{
	private readonly IClientManagementService _clientManagementService;

	public GetClientsHandler(IClientManagementService clientManagementService)
	{
		_clientManagementService = clientManagementService;
	}

	public async Task<GetClientsResult> Handle(GetClientsQuery request, CancellationToken cancellationToken)
	{
		var clients = await _clientManagementService.GetClientsAsync(request.KeysetPaginationRequest, cancellationToken);
		return new GetClientsResult(clients);
	}
}
