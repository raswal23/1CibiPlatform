namespace ATS.Features.Web.EmailProcessManagement.Query.GetEmailProcesses;

public record GetEmailProcessesQuery() : IQuery<GetEmailProcessesResult>;

public record GetEmailProcessesResult(IReadOnlyList<EmailProcessDetailsDTO> emailProcesses);

public class GetEmailProcessesHandler : IQueryHandler<GetEmailProcessesQuery, GetEmailProcessesResult>
{
	private readonly IEmailProcessManagementService _emailProcessManagementService;

	public GetEmailProcessesHandler(IEmailProcessManagementService emailProcessManagementService)
	{
		_emailProcessManagementService = emailProcessManagementService;
	}

	public async Task<GetEmailProcessesResult> Handle(
		GetEmailProcessesQuery request,
		CancellationToken cancellationToken)
	{
		var emailProcesses = await _emailProcessManagementService.GetEmailProcessesAsync(cancellationToken);

		return new GetEmailProcessesResult(emailProcesses);
	}
}
