namespace ATS.Features.Web.AIAssistant.Query.SearchOrdersBySubject;

public record SearchOrdersBySubjectQueryRequest(string Name)
	: IQuery<SearchOrdersBySubjectQueryResult>;

public record SearchOrdersBySubjectQueryResult(IReadOnlyList<AtsOrderSummaryDTO> Orders);

public class SearchOrdersBySubjectQueryRequestValidator
	: AbstractValidator<SearchOrdersBySubjectQueryRequest>
{
	public SearchOrdersBySubjectQueryRequestValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.WithMessage("A candidate name is required.")
			.MaximumLength(100)
			.WithMessage("The candidate name must not exceed 100 characters.");
	}
}

public class SearchOrdersBySubjectHandler
	: IQueryHandler<SearchOrdersBySubjectQueryRequest, SearchOrdersBySubjectQueryResult>
{
	private readonly IAtsAssistantService _assistantService;

	public SearchOrdersBySubjectHandler(IAtsAssistantService assistantService)
	{
		_assistantService = assistantService;
	}

	public async Task<SearchOrdersBySubjectQueryResult> Handle(
		SearchOrdersBySubjectQueryRequest request,
		CancellationToken cancellationToken)
	{
		var orders = await _assistantService.SearchOrdersBySubjectAsync(
			request.Name,
			cancellationToken);

		return new SearchOrdersBySubjectQueryResult(orders);
	}
}
