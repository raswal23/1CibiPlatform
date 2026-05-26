public interface IObjectStorageService
{
	Task<string> UploadAsync(Stream stream, string fileName, CancellationToken ct = default);
	Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default);
	Task DeleteAsync(string objectKey, CancellationToken ct = default);
	Task<string> GenerateDownloadUrlAsync(string objectKey, TimeSpan expiry);
}