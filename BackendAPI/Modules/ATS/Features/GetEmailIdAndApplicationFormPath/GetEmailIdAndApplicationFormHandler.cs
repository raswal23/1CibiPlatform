namespace ATS.Features.GetEmailIdAndApplicationFormPath;

public record GetEmailIdAndApplicationFormHandlerRequest(string HashToken) : IQuery<GetEmailIdAndApplicationFormResult>;

public record GetEmailIdAndApplicationFormResult(EmailIdAndApplicationFormPathDTO EmailIdAndApplicationFormPath);

public class GetEmailIdAndApplicationFormHandler : IQueryHandler<GetEmailIdAndApplicationFormHandlerRequest, GetEmailIdAndApplicationFormResult>
{
	private readonly IApplicationFormService _applicationFormService;

	public GetEmailIdAndApplicationFormHandler(IApplicationFormService applicationFormService)
	{
		_applicationFormService = applicationFormService;
	}
	public async Task<GetEmailIdAndApplicationFormResult> Handle(GetEmailIdAndApplicationFormHandlerRequest request, CancellationToken cancellationToken)
	{
		var emailIdAndApplicationFormPath = await _applicationFormService.GetEmailIdAndApplicationFormPathAsync(request.HashToken, cancellationToken);
		return new GetEmailIdAndApplicationFormResult(emailIdAndApplicationFormPath);
	}
}
