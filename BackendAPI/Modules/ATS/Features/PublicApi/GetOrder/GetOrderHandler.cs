namespace ATS.Features.PublicApi.GetOrder;

public record GetOrderQueryRequest(Guid OrderId) : IQuery<GetOrderQueryResult>;

public record GetOrderQueryResult(PublicOrderDetailDTO Order);

public class GetOrderQueryRequestValidator : AbstractValidator<GetOrderQueryRequest>
{
	public GetOrderQueryRequestValidator()
	{
		RuleFor(x => x.OrderId)
			.NotEmpty().WithMessage("Order ID is required.");
	}
}

public class GetOrderHandler : IQueryHandler<GetOrderQueryRequest, GetOrderQueryResult>
{
	private readonly IPublicApiService _publicApiService;

	public GetOrderHandler(IPublicApiService publicApiService)
	{
		_publicApiService = publicApiService;
	}

	public async Task<GetOrderQueryResult> Handle(
		GetOrderQueryRequest request,
		CancellationToken cancellationToken)
	{
		var order = await _publicApiService.GetOrderAsync(request.OrderId, cancellationToken);

		return new GetOrderQueryResult(order);
	}
}
