using Aliyun.OSS;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Storage;

public sealed class AlibabaOssStorageService : IObjectStorageService
{
	private readonly OssClient _client;
	private readonly IConfiguration _configuration;
	private readonly string OssBucket = string.Empty;
	private readonly string _atsTestFolder;
	public AlibabaOssStorageService(
		OssClient client,
		IConfiguration configuration)
	{
		_client = client;
		_configuration = configuration;
		OssBucket = _configuration
					 .GetSection("AlibabaOss")
					 .GetValue<string>("BucketName") ?? "one-cibi";
		_atsTestFolder = _configuration
						.GetSection("AlibabaOss")
						.GetValue<string>("ATSTestFolder") ?? string.Empty;
	}

	public async Task<string> UploadAsync(
		Stream stream, 
		string fileName, 
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(stream);

		var objectKey = string.IsNullOrEmpty(_atsTestFolder)
			? $"uploads/{Guid.CreateVersion7():N}-{fileName}"
			: $"{_atsTestFolder.TrimEnd('/')}/{fileName}";

		await Task.Run(() => { 
			ct.ThrowIfCancellationRequested(); 
			_client.PutObject(
				OssBucket, 
				objectKey, 
				stream); 
		}, ct);

		return objectKey;
	}

	public async Task<Stream> DownloadAsync(
		string objectKey,
		CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

		var result = await Task.Run(() =>
		{
			ct.ThrowIfCancellationRequested();

			return _client.GetObject(
				OssBucket,
				objectKey);
		}, ct);

		// caller disposes stream
		return result.Content;
	}

	public async Task DeleteAsync(
		string objectKey,
		CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

		await Task.Run(() =>
		{
			ct.ThrowIfCancellationRequested();

			_client.DeleteObject(
				OssBucket,
				objectKey);
		}, ct);
	}

	public Task<string> GenerateDownloadUrlAsync(
		string objectKey,
		TimeSpan expiry)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

		var expiration = DateTime.UtcNow.Add(expiry);

		var uri = _client.GeneratePresignedUri(
			OssBucket,
			objectKey,
			expiration);

		return Task.FromResult(uri.ToString());
	}
}