namespace ATS.Features.ClientManagement.Command.EditClient;

public record EditClientCommand(EditClientDTO editClient) : ICommand<EditClientResult>;

public class EditClientCommandValidator : AbstractValidator<EditClientCommand>
{
	public EditClientCommandValidator()
	{
		RuleFor(x => x.editClient)
			.NotNull().WithMessage("Edit client data is required.");

		When(x => x.editClient != null, () =>
		{
			RuleFor(x => x.editClient.ClientId)
				.NotEmpty().WithMessage("ClientId is required.");
			RuleFor(x => x.editClient.ClientName)
				.NotEmpty().WithMessage("ClientName is required.")
				.MaximumLength(100).WithMessage("ClientName cannot exceed 100 characters.");
			RuleFor(x => x.editClient.IsActive)
				.NotNull().WithMessage("IsActive is required.");
		});
	}
}

public record EditClientResult(ClientDetailsDTO client);

public class EditClientHandler : ICommandHandler<EditClientCommand, EditClientResult>
{
	private readonly IClientManagementService _clientManagementService;

	public EditClientHandler(IClientManagementService clientManagementService)
	{
		_clientManagementService = clientManagementService;
	}

	public async Task<EditClientResult> Handle(EditClientCommand request, CancellationToken cancellationToken)
	{
		var editedClient = await _clientManagementService.EditClientAsync(request.editClient);
		return new EditClientResult(editedClient);
	}
}
