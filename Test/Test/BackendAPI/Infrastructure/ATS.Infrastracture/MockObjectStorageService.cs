namespace Test.BackendAPI.Infrastructure.ATS.Infrastracture;

/// <summary>
/// Mock implementation of IObjectStorageService for integration tests.
/// Stores files in memory instead of contacting Alibaba OSS.
/// </summary>
public class MockObjectStorageService : IObjectStorageService
{
	private readonly Dictionary<string, byte[]> _storage = new();

	public async Task<string> UploadAsync(
		string folderName,
		string fileName,
		Stream stream,
		CancellationToken ct = default)
	{
		// Construct the key the same way the service would
		var key = $"{folderName}/{fileName}";

		// Read stream into memory
		using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream, ct);

		// Store in memory
		_storage[key] = memoryStream.ToArray();

		return key;
	}

	public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
	{
		if (!_storage.TryGetValue(key, out var fileContent))
		{
			throw new Exception($"The specified key does not exist: {key}");
		}

		// Return a new memory stream with the stored content
		return await Task.FromResult(new MemoryStream(fileContent));
	}

	public async Task DeleteAsync(string key, CancellationToken ct = default)
	{
		if (_storage.ContainsKey(key))
		{
			_storage.Remove(key);
		}

		await Task.CompletedTask;
	}

	public Task<string> GenerateDownloadUrlAsync(string objectKey, TimeSpan expiry)
	{
		// For testing purposes, return a mock URL
		return Task.FromResult($"mock://storage/{objectKey}");
	}

	/// <summary>
	/// Clears all stored files. Useful for test cleanup.
	/// </summary>
	public void Clear()
	{
		_storage.Clear();
	}
}
