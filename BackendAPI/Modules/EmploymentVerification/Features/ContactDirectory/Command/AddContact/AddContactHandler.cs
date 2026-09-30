namespace EmploymentVerification.Features.ContactDirectory.Command.AddContact;

public sealed record AddContactCommand(
	AddEmploymentVerificationContactDTO Contact)
	: ICommand<AddContactResult>;

public sealed record AddContactResult(bool IsAdded);

public sealed class AddContactCommandValidator : AbstractValidator<AddContactCommand>
{
	public AddContactCommandValidator()
	{
		RuleFor(command => command.Contact)
			.NotNull();

		RuleFor(command => command.Contact.CompanyName)
			.NotEmpty()
			.MaximumLength(150)
			// The keyset cursor is base64("company|id"), and CursorCodec splits on '|'.
			// A company name containing one shifts the field count so the cursor decodes
			// as null, which silently pins the user to the first page from that row on.
			.Must(ContactRules.HasNoCursorDelimiter)
			.WithMessage(ContactRules.CursorDelimiterMessage);

		RuleFor(command => command.Contact.EmailAddress)
			.NotEmpty()
			.EmailAddress()
			.MaximumLength(320);
	}
}

public sealed class AddContactHandler(IContactDirectoryService service)
	: ICommandHandler<AddContactCommand, AddContactResult>
{
	public async Task<AddContactResult> Handle(
		AddContactCommand request,
		CancellationToken cancellationToken)
	{
		var isAdded = await service.AddContactAsync(request.Contact, cancellationToken);

		return new AddContactResult(isAdded);
	}
}
