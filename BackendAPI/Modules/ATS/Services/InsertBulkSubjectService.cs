namespace ATS.Services;

public class InsertBulkSubjectService : IInsertBulkSubjectService
{
	private readonly ILogger<InsertBulkSubjectService> _logger;
	private readonly IATSRepository _atsRepository;
	private readonly IObjectStorageService _objectStorageService;
	private readonly IUnitOfWork _unitOfWork;
	private readonly ISecureToken _securetoken;
	private readonly IConfiguration _configuration;
	private readonly IHashService _hashService;

	public InsertBulkSubjectService(
		ILogger<InsertBulkSubjectService> logger,
		IATSRepository atsRepository,
		IObjectStorageService objectStorageService,
		IConfiguration configuration,
		IHashService hashService,
		ISecureToken secureToken,
		IUnitOfWork unitOfWork)
	{
		_logger = logger;
		_atsRepository = atsRepository;
		_objectStorageService = objectStorageService;
		_configuration = configuration;
		_securetoken = secureToken;
		_hashService = hashService;
		_unitOfWork = unitOfWork;
	}

	public async Task<Guid> InsertBulkSubjectAsync(BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO, CancellationToken ct = default)
	{

		_logger.LogInformation("Starting uploading process for file {FileName}", bulkUploadFileDetailsDTO.FileName);

		string bulkFileKey = "";
		string folderName = "ATS Objects";

		var entity = bulkUploadFileDetailsDTO.Adapt<BulkUploadFileDetails>();
		entity.FileID = Guid.NewGuid();
		entity.Status = "Uploaded";
		entity.DateCreated = DateTime.UtcNow;

		var logContext = new
		{
			Action = "UploadFile",
			Step = "StartUploading",
			Identity = entity.FileID,
			Timestamp = DateTime.UtcNow
		};

		try
		{
			if (bulkUploadFileDetailsDTO.BulkFile != null)
			{
				await using var fileStream = bulkUploadFileDetailsDTO.BulkFile.OpenReadStream();
				
				bulkFileKey = await _objectStorageService.UploadAsync(
					folderName, 
					bulkUploadFileDetailsDTO.BulkFile.FileName, 
					fileStream, 
					ct);

				if (string.IsNullOrEmpty(bulkFileKey))
				{
					_logger.LogError("Failed to upload file to object storage {FileID} : {@Context}", bulkUploadFileDetailsDTO.FileID, logContext);
					throw new Exception("Failed to upload file to Object Storage");
				}

				entity.FileName = bulkFileKey;
			}

			if (bulkUploadFileDetailsDTO.BulkFile == null)
			{
				_logger.LogError("Bulk file is eampty.{FileID} : {@Context}", bulkUploadFileDetailsDTO.FileID, logContext);
				throw new ValidationException("Bulk file is required.");
			}

			await _unitOfWork.BeginTransactionAsync(ct);

			await _atsRepository.AddBulkUploadFileDetailsAsync(entity);

			await _unitOfWork.CommitAsync(ct);

			_logger.LogInformation("Successfully added the file info in the database and object storage - {FileID}: {@Context}", bulkUploadFileDetailsDTO.FileID, logContext);

			return entity.FileID;

		}

		catch (Exception ex)
		{
			await _unitOfWork.RollbackAsync(ct);
			_logger.LogError(ex, "Failed to insert data for Bulk File Information {FileID} : {@Context}", bulkUploadFileDetailsDTO.FileID, logContext);
			throw new InternalServerException($"Failed insert data to the database. {ex.InnerException?.Message ?? ex.Message}");
		}

	}
}
