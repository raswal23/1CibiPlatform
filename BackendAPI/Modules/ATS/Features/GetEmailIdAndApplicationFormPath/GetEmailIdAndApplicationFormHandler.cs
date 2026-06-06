namespace ATS.Features.GetEmailIdAndApplicationFormPath;

public record GetEmailIdAndApplicationFormHandlerRequest(string HashToken) : IQuery<GetEmailIdAndApplicationFormResult>;

public record GetEmailIdAndApplicationFormResult(EmailIdAndApplicationFormPathDTO EmailIdAndApplicationFormPath);

public class GetEmailIdAndApplicationFormHandler : IQueryHandler<GetEmailIdAndApplicationFormHandlerRequest, GetEmailIdAndApplicationFormResult>
{
	private readonly IATSService _atsService;

	public GetEmailIdAndApplicationFormHandler(IATSService atsService)
	{
		_atsService = atsService;
	}
	public async Task<GetEmailIdAndApplicationFormResult> Handle(GetEmailIdAndApplicationFormHandlerRequest request, CancellationToken cancellationToken)
	{
		var emailIdAndApplicationFormPath = await _atsService.GetEmailIdAndApplicationFormPathAsync(request.HashToken, cancellationToken);
		return new GetEmailIdAndApplicationFormResult(emailIdAndApplicationFormPath);
	}
}
