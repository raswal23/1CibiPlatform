namespace ATS.Features.ClientManagement.Command.AddClient;

public record AddClientCommand(AddClientDTO client) : ICommand<AddClientResult>;

public record AddClientResult(bool isAdded);

public class AddClientCommandValidator : AbstractValidator<AddClientCommand>
{
	public AddClientCommandValidator()
	{
		RuleFor(x => x.client)
			.NotNull().WithMessage("Client data is required.");

		When(x => x.client != null, () =>
		{
			RuleFor(x => x.client.ClientName)
				.NotEmpty().WithMessage("ClientName is required.");
			RuleFor(x => x.client.IsActive)
				.NotEmpty().WithMessage("IsActive is required.");
		});
	}
}

public class AddClientHandler : ICommandHandler<AddClientCommand, AddClientResult>
{
	private readonly IClientManagementService _clientManagementService;

	public AddClientHandler(IClientManagementService clientManagementService)
	{
		_clientManagementService = clientManagementService;
	}

	public async Task<AddClientResult> Handle(AddClientCommand request, CancellationToken cancellationToken)
	{
		var addedClient = await _clientManagementService.AddClientAsync(request.client);
		return new AddClientResult(addedClient);
	}
}
