namespace EmploymentVerification.Features.VerificationRequests.Command.CreateRequest;

public sealed record CreateRequestCommand(
	CreateEmploymentVerificationRequest Request)
	: ICommand<EmploymentVerificationRequest>;

public sealed class CreateRequestCommandValidator
	: AbstractValidator<CreateRequestCommand>
{
	public CreateRequestCommandValidator()
	{
		RuleFor(command => command.Request)
			.NotNull();

		RuleFor(command => command.Request.AtsSubjectId)
			.NotNull()
			.WithMessage("An ATS subject is required.");

		RuleFor(command => command.Request.CandidateName)
			.NotEmpty()
			.MaximumLength(200);

		RuleFor(command => command.Request.PreviousEmployer)
			.NotEmpty()
			.MaximumLength(250);

		RuleFor(command => command.Request.Position)
			.NotEmpty()
			.MaximumLength(200);

		RuleFor(command => command.Request.HrEmail)
			.NotEmpty()
			.EmailAddress()
			.MaximumLength(320);

		RuleFor(command => command.Request.EmploymentEndDate)
			.GreaterThanOrEqualTo(command => command.Request.EmploymentStartDate)
			.When(command =>
				command.Request.EmploymentStartDate.HasValue &&
				command.Request.EmploymentEndDate.HasValue)
			.WithMessage("Employment end date must be on or after the start date.");
	}
}

public sealed class CreateRequestHandler(
	IEmploymentVerificationService service)
	: ICommandHandler<CreateRequestCommand, EmploymentVerificationRequest>
{
	public Task<EmploymentVerificationRequest> Handle(
		CreateRequestCommand request,
		CancellationToken cancellationToken) =>
		service.CreateAndSendAsync(request.Request, cancellationToken);
}
