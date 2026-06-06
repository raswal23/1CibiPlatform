namespace PhilSys.Features.UpdateFaceLivenessSession;
public record UpdateFaceLivenessSessionCommand(string HashToken, string FaceLivenessSessionId) : ICommand<UpdateFaceLivenessSessionResult>;

public record UpdateFaceLivenessSessionResult(VerificationResponseDTO VerificationResponseDTO);
public class UpdateFaceLivenessSessionHandler : ICommandHandler<UpdateFaceLivenessSessionCommand, UpdateFaceLivenessSessionResult>
{
	private readonly UpdateFaceLivenessSessionService _updateFaceLivenessSessionService;
	public UpdateFaceLivenessSessionHandler(UpdateFaceLivenessSessionService UpdateFaceLivenessSessionService)
	{
		_updateFaceLivenessSessionService = UpdateFaceLivenessSessionService;
	}
	public async Task<UpdateFaceLivenessSessionResult> Handle(UpdateFaceLivenessSessionCommand command, CancellationToken cancellationToken)
	{
		var result = await _updateFaceLivenessSessionService.UpdateFaceLivenessSessionAsync(
				command.HashToken,
				command.FaceLivenessSessionId
			);
		return new UpdateFaceLivenessSessionResult(result);
	}
}
