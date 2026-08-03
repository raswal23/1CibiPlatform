namespace PhilSys.Features.GetLivenessKey;

public record GetLivenessKeyQueryRequest() : IRequest<GetLivenessKeyResult>;

public record GetLivenessKeyResult(string LivenessKey);
public class GetLivenessKeyHandler : IRequestHandler<GetLivenessKeyQueryRequest, GetLivenessKeyResult>
{
	private readonly IGetLivenessKeyService _getLivenessKeyService;

	public GetLivenessKeyHandler(IGetLivenessKeyService getLivenessKeyService)
	{
		_getLivenessKeyService = getLivenessKeyService;
	}
	public async Task<GetLivenessKeyResult> Handle(GetLivenessKeyQueryRequest request, CancellationToken cancellationToken)
	{
		var livenessKey = await _getLivenessKeyService.GetLivenessKey();
		return new GetLivenessKeyResult(livenessKey);
	}
}
