namespace ATS.Features.Web.EmailAccountManagement.Query.GetEmailAccounts;

public record GetEmailAccountsQuery() : IQuery<GetEmailAccountsResult>;

public record GetEmailAccountsResult(List<EmailAccountDTO> emailAccounts);

public class GetEmailAccountsHandler : IQueryHandler<GetEmailAccountsQuery, GetEmailAccountsResult>
{
	private readonly IAtsEmailAccountManagementService _emailAccountManagementService;

	public GetEmailAccountsHandler(IAtsEmailAccountManagementService emailAccountManagementService)
	{
		_emailAccountManagementService = emailAccountManagementService;
	}

	public async Task<GetEmailAccountsResult> Handle(
		GetEmailAccountsQuery request,
		CancellationToken cancellationToken)
	{
		var accounts = await _emailAccountManagementService.GetAccountsAsync(cancellationToken);

		return new GetEmailAccountsResult(accounts);
	}
}
