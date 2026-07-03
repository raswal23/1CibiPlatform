namespace PhilSys.Features.PostPCN;
public record PostPCNCommand(string value,
							 string bearer_token,	
							 string face_liveness_session_id) : ICommand<PostPCNResult>;
public record PostPCNResult(BasicInformationOrPCNResponseDTO PCNResponseDTO);
public class PostPCNCommandValidator : AbstractValidator<PostPCNCommand>
{
	public PostPCNCommandValidator()
	{
		RuleFor(x => x.value)
			.NotEmpty().WithMessage("PCN value is required.");

		RuleFor(x => x.bearer_token)
			.NotEmpty().WithMessage("bearer_token is required.");
	}
}

public class PostPCNHandler : ICommandHandler<PostPCNCommand, PostPCNResult>
{
	private readonly IPhilSysService _philsysService;
	public PostPCNHandler(IPhilSysService philsysService)
	{
		_philsysService = philsysService;
	}
	public async Task<PostPCNResult> Handle(PostPCNCommand command, CancellationToken cancellationToken)
	{
		var result = await _philsysService.PostPCNAsync(
				command.value,
				command.bearer_token,
				command.face_liveness_session_id
			);
		return new PostPCNResult(result);
	}
}
