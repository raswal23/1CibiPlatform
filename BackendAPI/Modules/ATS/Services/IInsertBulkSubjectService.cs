namespace ATS.Services;

public interface IInsertBulkSubjectService
{
	Task<Guid> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO, CancellationToken ct = default);

}
