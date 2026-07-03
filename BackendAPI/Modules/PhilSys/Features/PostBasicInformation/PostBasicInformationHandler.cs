namespace PhilSys.Features.PostBasicInformation;
public record PostBasicInformationCommand(string first_name,
										  string middle_name,
										  string last_name,
										  string suffix,
										  string birth_date,
										  string bearer_token,
										  string face_liveness_session_id) : ICommand<PostBasicInformationResult>;
public record PostBasicInformationResult(BasicInformationOrPCNResponseDTO BasicInformationResponseDTO);
public class PostBasicInformationCommandValidator : AbstractValidator<PostBasicInformationCommand>
{
	public PostBasicInformationCommandValidator()
	{
		RuleFor(x => x.first_name)
			.NotEmpty().WithMessage("first_name is required.")
			.MaximumLength(100).WithMessage("first_name must not exceed 100 characters.");

		RuleFor(x => x.last_name)
			.NotEmpty().WithMessage("last_name is required.")
			.MaximumLength(100).WithMessage("last_name must not exceed 100 characters.");

		RuleFor(x => x.birth_date)
			.NotEmpty().WithMessage("birth_date is required.")
			.Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("birth_date must be in format YYYY-MM-DD.");

		RuleFor(x => x.bearer_token)
			.NotEmpty().WithMessage("bearer_token is required.");
	}
}

public class PostBasicInformationHandler : ICommandHandler<PostBasicInformationCommand, PostBasicInformationResult>
{
	private readonly IPhilSysService _philsysService;
	public PostBasicInformationHandler(IPhilSysService philsysService)
	{
		_philsysService = philsysService;
	}
	public async Task<PostBasicInformationResult> Handle(PostBasicInformationCommand command, CancellationToken cancellationToken)
	{
		var result = await _philsysService.PostBasicInformationAsync(
				command.first_name,
				command.middle_name,
				command.last_name,
				command.suffix,
				command.birth_date,
				command.bearer_token,
				command.face_liveness_session_id
			);
		return new PostBasicInformationResult(result);
	}
}
