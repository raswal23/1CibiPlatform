namespace ATS.Features.PublicApi.GetBulkUploadStatus;

public record GetBulkUploadStatusQueryRequest(Guid FileId) : IQuery<GetBulkUploadStatusQueryResult>;

public record GetBulkUploadStatusQueryResult(PublicBulkUploadStatusDTO Upload);

public class GetBulkUploadStatusQueryRequestValidator : AbstractValidator<GetBulkUploadStatusQueryRequest>
{
	public GetBulkUploadStatusQueryRequestValidator()
	{
		RuleFor(x => x.FileId)
			.NotEmpty().WithMessage("File ID is required.");
	}
}

public class GetBulkUploadStatusHandler
	: IQueryHandler<GetBulkUploadStatusQueryRequest, GetBulkUploadStatusQueryResult>
{
	private readonly IPublicApiService _publicApiService;

	public GetBulkUploadStatusHandler(IPublicApiService publicApiService)
	{
		_publicApiService = publicApiService;
	}

	public async Task<GetBulkUploadStatusQueryResult> Handle(
		GetBulkUploadStatusQueryRequest request,
		CancellationToken cancellationToken)
	{
		var upload = await _publicApiService.GetBulkUploadStatusAsync(request.FileId, cancellationToken);

		return new GetBulkUploadStatusQueryResult(upload);
	}
}
