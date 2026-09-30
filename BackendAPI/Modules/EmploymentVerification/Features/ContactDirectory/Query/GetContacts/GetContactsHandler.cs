namespace EmploymentVerification.Features.ContactDirectory.Query.GetContacts;

public sealed record GetContactsQuery(
	string? Cursor = null,
	int? PageSize = 10,
	string? SearchTerm = null)
	: IQuery<GetContactsResult>;

public sealed record GetContactsResult(
	KeysetPaginatedResult<EmploymentVerificationContactDTO> Contacts);

public sealed class GetContactsQueryValidator : AbstractValidator<GetContactsQuery>
{
	public GetContactsQueryValidator()
	{
		RuleFor(query => query.PageSize)
			.Must(pageSize => pageSize is null || (pageSize > 0 && pageSize <= KeysetPage.MaxPageSize))
			.WithMessage($"PageSize must be greater than 0 and less than or equal to {KeysetPage.MaxPageSize}.");
	}
}

public sealed class GetContactsHandler(IContactDirectoryService service)
	: IQueryHandler<GetContactsQuery, GetContactsResult>
{
	public async Task<GetContactsResult> Handle(
		GetContactsQuery request,
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			request.Cursor,
			request.PageSize ?? 10,
			request.SearchTerm);

		var contacts = await service.GetContactsAsync(paginationRequest, cancellationToken);

		return new GetContactsResult(contacts);
	}
}
