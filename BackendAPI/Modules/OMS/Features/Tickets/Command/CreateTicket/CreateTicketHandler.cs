namespace OMS.Features.Tickets.Command.CreateTicket;

public sealed record CreateTicketCommand(
	CreateOMSTicketRequest Request)
	: ICommand<OMSTicketCreated>;

public sealed class CreateTicketCommandValidator
	: AbstractValidator<CreateTicketCommand>
{
	private static readonly char[] ForbiddenNameCharacters =
		"\\|!#$%&/()=+`~:?»«@£§€{}[];*'<>_,".ToCharArray();

	public CreateTicketCommandValidator()
	{
		RuleFor(command => command.Request)
			.NotNull()
			.WithMessage("Ticket data is required.");

		When(command => command.Request != null, () =>
		{
			RuleFor(command => command.Request.FirstName)
				.NotEmpty()
				.Must(BeAValidName)
				.WithMessage("First name must not contain digits or special characters.");

			RuleFor(command => command.Request.MiddleName)
				.Must(BeAValidName)
				.WithMessage("Middle name must not contain digits or special characters.")
				.When(command => !string.IsNullOrEmpty(command.Request.MiddleName));

			RuleFor(command => command.Request.LastName)
				.NotEmpty()
				.Must(BeAValidName)
				.WithMessage("Last name must not contain digits or special characters.");

			RuleFor(command => command.Request.EmailAddress)
				.NotEmpty()
				.EmailAddress();

			RuleFor(command => command.Request.PhoneNumber)
				.NotEmpty()
				.Length(11, 12)
				.Must(value => value.All(char.IsDigit))
				.WithMessage("Phone number must contain digits only.");

			RuleFor(command => command.Request.TurnAroundTimeID)
				.GreaterThan(0);

			RuleFor(command => command.Request.ReportTypeID)
				.GreaterThan(0);

			RuleFor(command => command.Request.RequestorFirstName)
				.NotEmpty();

			RuleFor(command => command.Request.RequestorLastName)
				.NotEmpty();

			RuleFor(command => command.Request.RequestorEmailAddress)
				.NotEmpty()
				.EmailAddress();

			RuleFor(command => command.Request.Site)
				.NotEmpty();

			RuleFor(command => command.Request.SSSIDNumber)
				.Matches("^[0-9]{10}$")
				.WithMessage("SSS number must contain exactly 10 digits.")
				.When(command => !string.IsNullOrEmpty(command.Request.SSSIDNumber));

			RuleFor(command => command.Request.TIN)
				.Matches("^[0-9]{12}$")
				.WithMessage("TIN must contain exactly 12 digits.")
				.When(command => !string.IsNullOrEmpty(command.Request.TIN));

			RuleFor(command => command.Request.PostalCode)
				.Must(value => value!.All(char.IsDigit))
				.WithMessage("Postal code must contain digits only.")
				.When(command => !string.IsNullOrEmpty(command.Request.PostalCode));
		});
	}

	private static bool BeAValidName(string? value) =>
		string.IsNullOrEmpty(value) ||
		(!value.Any(char.IsDigit) && value.IndexOfAny(ForbiddenNameCharacters) < 0);
}

public sealed class CreateTicketHandler(
	IOMSTicketCreator ticketCreator)
	: ICommandHandler<CreateTicketCommand, OMSTicketCreated>
{
	public Task<OMSTicketCreated> Handle(
		CreateTicketCommand request,
		CancellationToken cancellationToken) =>
		ticketCreator.CreateTicketAsync(request.Request, cancellationToken);
}
