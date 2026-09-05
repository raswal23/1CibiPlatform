namespace FrontendWebassembly.Validation.CheckBulkFileName;

public sealed class CheckBulkFileName
{
	private readonly IBulkUploadService _bulkUploadService;

	public CheckBulkFileName(IBulkUploadService bulkUploadService) =>
		_bulkUploadService = bulkUploadService;

	public async Task<string?> ValidateAsync(string? fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return "File name is required.";

		var response = await _bulkUploadService.GetBulkUploadsAsync(
			pageSize: 100,
			searchTerm: fileName.Trim());

		if (!response.IsSuccess || response.Data is null)
			return response.ErrorDetail;

		return response.Data.Items.Any(upload =>
			string.Equals(upload.FileName, fileName.Trim(), StringComparison.OrdinalIgnoreCase))
			? "A file with this name has already been uploaded."
			: null;
	}
}
