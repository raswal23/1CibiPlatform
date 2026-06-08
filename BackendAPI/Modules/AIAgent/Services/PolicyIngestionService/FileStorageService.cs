namespace AIAgent.Services.PolicyIngestion;

public class FileStorageService : IFileStorageService
{
	private readonly ILogger<FileStorageService> _logger;
	private readonly string _storageDirectory;
	private const string BaseUrl = "/api/files";

	public FileStorageService(ILogger<FileStorageService> logger, IConfiguration configuration)
	{
		_logger = logger;
		_storageDirectory = configuration.GetValue<string>("FileStorage:Directory") ?? System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files");

		if (!Directory.Exists(_storageDirectory))
		{
			Directory.CreateDirectory(_storageDirectory);
			_logger.LogInformation("Created storage directory at: {Directory}", _storageDirectory);
		}
	}

	public async Task<string> SaveFileAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
	{
		var uniqueFileName = $"{Guid.CreateVersion7()}_{fileName}";
		var filePath = System.IO.Path.Combine(_storageDirectory, uniqueFileName);

		await File.WriteAllBytesAsync(filePath, fileBytes, cancellationToken);

		_logger.LogInformation("Saved file: {FileName} at {Path}", uniqueFileName, filePath);

		return uniqueFileName;
	}

	public string GetFileUrl(string fileName)
	{
		return $"{BaseUrl}/{fileName}";
	}
}
