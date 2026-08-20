namespace ATS.Features.ClientManagement.Command.EditClient;

public record EditClientCommand(IReadOnlyCollection<EditClientDTO> editClients) : ICommand<EditClientResult>;

public class EditClientCommandValidator : AbstractValidator<EditClientCommand>
{
	public EditClientCommandValidator()
	{
		RuleFor(x => x.editClients)
			.Cascade(CascadeMode.Stop)
			.NotEmpty().WithMessage("At least one client/package assignment is required.")
			.Must(clients => clients.Select(c => c.PackageId).Distinct().Count() == clients.Count)
			.WithMessage("Duplicate package assignments are not allowed.")
			.Must(HaveConsistentClientDetails)
			.WithMessage("All package assignments must contain the same client details.");

		RuleForEach(x => x.editClients).ChildRules(client =>
		{
			client.RuleFor(x => x.ClientId)
				.GreaterThan(0).WithMessage("ClientId is required.");
			client.RuleFor(x => x.ClientName)
				.NotEmpty().WithMessage("ClientName is required.")
				.MaximumLength(100).WithMessage("ClientName cannot exceed 100 characters.");
			client.RuleFor(x => x.ClientDescription)
				.NotEmpty().WithMessage("ClientDescription is required.")
				.MaximumLength(500).WithMessage("ClientDescription cannot exceed 500 characters.");
			client.RuleFor(x => x.PackageId)
				.GreaterThan(0).WithMessage("PackageId is required.");
		});
	}

	private static bool HaveConsistentClientDetails(IReadOnlyCollection<EditClientDTO> clients)
	{
		if (clients.Count == 0)
			return true;

		var first = clients.First();
		return clients.All(client =>
			client.ClientId == first.ClientId &&
			client.ClientName == first.ClientName &&
			client.ClientDescription == first.ClientDescription &&
			client.IsActive == first.IsActive);
	}
}

public record EditClientResult(IReadOnlyList<ClientDetailsDTO> clients);

public class EditClientHandler : ICommandHandler<EditClientCommand, EditClientResult>
{
	private readonly IClientManagementService _clientManagementService;

	public EditClientHandler(IClientManagementService clientManagementService)
	{
		_clientManagementService = clientManagementService;
	}

	public async Task<EditClientResult> Handle(EditClientCommand request, CancellationToken cancellationToken)
	{
		var editedClient = await _clientManagementService.EditClientAsync(request.editClients, cancellationToken);
		return new EditClientResult(editedClient);
	}
}
