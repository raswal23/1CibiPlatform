namespace EmploymentVerification.Features.ContactDirectory.Command.EditContact;

public sealed record EditContactCommand(
	EditEmploymentVerificationContactDTO Contact)
	: ICommand<EditContactResult>;

public sealed record EditContactResult(bool IsUpdated);

public sealed class EditContactCommandValidator : AbstractValidator<EditContactCommand>
{
	public EditContactCommandValidator()
	{
		RuleFor(command => command.Contact)
			.NotNull();

		RuleFor(command => command.Contact.Id)
			.NotEmpty()
			.WithMessage("A contact id is required.");

		RuleFor(command => command.Contact.CompanyName)
			.NotEmpty()
			.MaximumLength(150)
			.Must(ContactRules.HasNoCursorDelimiter)
			.WithMessage(ContactRules.CursorDelimiterMessage);

		RuleFor(command => command.Contact.EmailAddress)
			.NotEmpty()
			.EmailAddress()
			.MaximumLength(320);
	}
}

public sealed class EditContactHandler(IContactDirectoryService service)
	: ICommandHandler<EditContactCommand, EditContactResult>
{
	public async Task<EditContactResult> Handle(
		EditContactCommand request,
		CancellationToken cancellationToken)
	{
		var isUpdated = await service.EditContactAsync(request.Contact, cancellationToken);

		return new EditContactResult(isUpdated);
	}
}
