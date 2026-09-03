
namespace ATS.Features.Web.WithdrawnApplicationForm;

public record WithdrawnApplicationFormCommand(string HashToken) : ICommand<WithdrawnApplicationFormResult>;

public record WithdrawnApplicationFormResult(bool isEdited);

public class WithdrawnApplicationFormCommandValidator : AbstractValidator<WithdrawnApplicationFormCommand>
{
	public WithdrawnApplicationFormCommandValidator()
	{
		RuleFor(x => x.HashToken)
			.NotEmpty()
			.WithMessage("Hash token is required.");
	}
}
public class WithdrawnApplicationFormHandler : ICommandHandler<WithdrawnApplicationFormCommand, WithdrawnApplicationFormResult>
{
	private readonly IApplicationFormService _applicationFormService;

	public WithdrawnApplicationFormHandler(IApplicationFormService applicationFormService)
	{
		_applicationFormService = applicationFormService;
	}
	public async Task<WithdrawnApplicationFormResult> Handle(
		WithdrawnApplicationFormCommand request,
		CancellationToken cancellationToken)
	{
		var isEdited = await _applicationFormService.WithdrawnApplicationForm(request.HashToken, cancellationToken);
		return new WithdrawnApplicationFormResult(isEdited);
	}
}
