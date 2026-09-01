namespace ATS.Features.Web.EmailInvitationRequest;

public record GetWithdrawnEmailInvitationRequestsQueryRequest(string? Cursor = null, int? PageSize = 10, string? SearchTerm = null) : IQuery<GetWithdrawnEmailInvitationRequestsQueryResult>;

public record GetWithdrawnEmailInvitationRequestsQueryResult(KeysetPaginatedResult<EmailInvitationRequestListDTO> Requests);

public class GetWithdrawnEmailInvitationRequestsRequestValidator : AbstractValidator<GetWithdrawnEmailInvitationRequestsQueryRequest>
{
	public GetWithdrawnEmailInvitationRequestsRequestValidator()
	{
		RuleFor(x => x.PageSize).Must(pageSize => pageSize > 0 && pageSize <= 100)
			.WithMessage("PageSize must be greater than 0.");
	}
}

public class GetWithdrawnEmailInvitationRequestsHandler : IQueryHandler<GetWithdrawnEmailInvitationRequestsQueryRequest, GetWithdrawnEmailInvitationRequestsQueryResult>
{
	private readonly IEndorsementSubmissionService _endorsementSubmissionService;

	public GetWithdrawnEmailInvitationRequestsHandler(IEndorsementSubmissionService endorsementSubmissionService)
	{
		_endorsementSubmissionService = endorsementSubmissionService;
	}

	public async Task<GetWithdrawnEmailInvitationRequestsQueryResult> Handle(GetWithdrawnEmailInvitationRequestsQueryRequest request, CancellationToken cancellationToken)
	{
		var KeysetPaginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var data = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(KeysetPaginationRequest, cancellationToken);

		return new GetWithdrawnEmailInvitationRequestsQueryResult(data);
	}
}
