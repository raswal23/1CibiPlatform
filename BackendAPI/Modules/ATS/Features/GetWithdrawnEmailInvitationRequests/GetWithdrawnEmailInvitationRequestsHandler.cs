namespace ATS.Features.EmailInvitationRequest;

public record GetWithdrawnEmailInvitationRequestsQueryRequest(int? PageNumber = 1, int? PageSize = 10, string? SearchTerm = null) : IQuery<GetWithdrawnEmailInvitationRequestsQueryResult>;

public record GetWithdrawnEmailInvitationRequestsQueryResult(PaginatedResult<EmailInvitationRequestListDTO> Requests);

public class GetWithdrawnEmailInvitationRequestsRequestValidator : AbstractValidator<GetWithdrawnEmailInvitationRequestsQueryRequest>
{
	public GetWithdrawnEmailInvitationRequestsRequestValidator()
	{
		RuleFor(x => x.PageNumber).Must(pageIndex => pageIndex >= 0)
			.WithMessage("PageNumber must be greater than 0.");

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
		var paginationRequest = new PaginationRequest(
			request.PageNumber ?? 1,
			request.PageSize ?? 10,
			request.SearchTerm);

		var data = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(paginationRequest, cancellationToken);

		return new GetWithdrawnEmailInvitationRequestsQueryResult(data);
	}
}
