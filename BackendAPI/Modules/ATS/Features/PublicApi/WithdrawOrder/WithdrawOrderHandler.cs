namespace ATS.Features.PublicApi.WithdrawOrder;

public record WithdrawOrderCommand(Guid OrderId) : ICommand<WithdrawOrderResult>;

public record WithdrawOrderResult(bool Success);

public class WithdrawOrderCommandValidator : AbstractValidator<WithdrawOrderCommand>
{
	public WithdrawOrderCommandValidator()
	{
		RuleFor(x => x.OrderId)
			.NotEmpty().WithMessage("Order ID is required.");
	}
}

public class WithdrawOrderHandler : ICommandHandler<WithdrawOrderCommand, WithdrawOrderResult>
{
	private readonly IPublicApiService _publicApiService;

	public WithdrawOrderHandler(IPublicApiService publicApiService)
	{
		_publicApiService = publicApiService;
	}

	public async Task<WithdrawOrderResult> Handle(
		WithdrawOrderCommand request,
		CancellationToken cancellationToken)
	{
		var success = await _publicApiService.WithdrawOrderAsync(request.OrderId, cancellationToken);

		return new WithdrawOrderResult(success);
	}
}
