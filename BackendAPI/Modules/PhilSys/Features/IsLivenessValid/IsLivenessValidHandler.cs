namespace PhilSys.Features.IsLivenessValid;
public record IsLivenessValidCommand(string HashToken) : ICommand<IsLivenessValidResult>;
public record IsLivenessValidResult(TransactionStatusResponseDTO TransactionStatusResponseDTO);
public class IsLivenessValidCommandValidator : AbstractValidator<IsLivenessValidCommand>
{
	public IsLivenessValidCommandValidator()
	{
		RuleFor(x => x.HashToken)
			.NotEmpty().WithMessage("HashToken is required.");
	}
}

public class IsLivenessValidHandler : ICommandHandler<IsLivenessValidCommand, IsLivenessValidResult>
{
	private readonly ILivenessSessionService _livenessSessionService;
	public IsLivenessValidHandler(ILivenessSessionService livenessSessionService)
	{
		_livenessSessionService = livenessSessionService;
	}
	public async Task<IsLivenessValidResult> Handle(IsLivenessValidCommand request, CancellationToken cancellationToken)
	{
		var isLivenessValid = await _livenessSessionService.IsLivenessUsedAsync(request.HashToken);
		return new IsLivenessValidResult(isLivenessValid);
	}
}
