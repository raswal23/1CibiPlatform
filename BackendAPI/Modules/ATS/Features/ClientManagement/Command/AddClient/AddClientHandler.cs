namespace ATS.Features.ClientManagement.Command.AddClient;

public record AddClientCommand(IReadOnlyCollection<AddClientDTO> clients) : ICommand<AddClientResult>;

public record AddClientResult(bool isAdded);

public class AddClientCommandValidator : AbstractValidator<AddClientCommand>
{
	public AddClientCommandValidator()
	{
		RuleFor(x => x.clients)
			.Cascade(CascadeMode.Stop)
			.NotEmpty().WithMessage("At least one client/package assignment is required.")
			.Must(clients => clients.Select(c => c.PackageId).Distinct().Count() == clients.Count)
			.WithMessage("Duplicate package assignments are not allowed.")
			.Must(HaveConsistentClientDetails)
			.WithMessage("All package assignments must contain the same client details.");

		RuleForEach(x => x.clients).ChildRules(client =>
		{
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

	private static bool HaveConsistentClientDetails(IReadOnlyCollection<AddClientDTO> clients)
	{
		if (clients.Count == 0)
			return true;

		var first = clients.First();
		return clients.All(client =>
			client.ClientName == first.ClientName &&
			client.ClientDescription == first.ClientDescription &&
			client.IsActive == first.IsActive);
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
		var addedClient = await _clientManagementService.AddClientAsync(request.clients, cancellationToken);
		return new AddClientResult(addedClient);
	}
}
