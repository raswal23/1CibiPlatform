namespace PhilSys.Features.GetPhilSysToken;
public record GetPhilSysTokenCommand(string client_id, string client_secret) : ICommand<GetCredentialResult>;
public record GetCredentialResult(string AccessToken);

public class GetPhilSysTokenCommandValidator : AbstractValidator<GetPhilSysTokenCommand>
{
	public GetPhilSysTokenCommandValidator()
	{
		RuleFor(x => x.client_id)
			.NotEmpty().WithMessage("client_id is required.");

		RuleFor(x => x.client_secret)
			.NotEmpty().WithMessage("client_secret is required.");
	}
}

public class GetPhilSysTokenHandler : ICommandHandler<GetPhilSysTokenCommand, GetCredentialResult>
{
	private readonly IPhilSysService _philsyService;
	public GetPhilSysTokenHandler(IPhilSysService philSysService)
	{
		_philsyService = philSysService;
	}
	public async Task<GetCredentialResult> Handle(GetPhilSysTokenCommand command, CancellationToken cancellationToken)
	{
		var tokenResult = await _philsyService.GetPhilsysTokenAsync(
			command.client_id,
			command.client_secret
		);
		return new GetCredentialResult(tokenResult);
	}
}
