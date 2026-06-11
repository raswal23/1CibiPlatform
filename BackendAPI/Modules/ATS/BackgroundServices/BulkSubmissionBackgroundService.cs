using ClosedXML.Excel;
using Microsoft.Extensions.Caching.Hybrid;

namespace ATS.BackgroundServices;

public class BulkSubmissionBackgroundService : BackgroundService
{
	private readonly ILogger<BulkSubmissionBackgroundService> _logger;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ISecureToken _secureToken;
	private readonly IHashService _hashService;
	private readonly IConfiguration _configuration;
	private readonly HybridCache _hybridCache;
	private readonly int _applicationFormExpiryInHours;

	public BulkSubmissionBackgroundService(IServiceScopeFactory scopeFactory,
										   ISecureToken secureToken,
										   IHashService hashService,
										   IConfiguration configuration,
										   ILogger<BulkSubmissionBackgroundService> logger,
										   HybridCache hybridCache)
	{
		_scopeFactory = scopeFactory;
		_secureToken = secureToken;
		_hashService = hashService;
		_configuration = configuration;
		_logger = logger;
		_applicationFormExpiryInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
		_hybridCache = hybridCache;
	}
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			using var scope = _scopeFactory.CreateScope();

			var repository = scope.ServiceProvider
				.GetRequiredService<IATSRepository>();

			var uploadFiles = scope.ServiceProvider
				.GetRequiredService<IObjectStorageService>();


			var pendingFiles =
				await repository.GetBulkUploadFileDetailsAsync();

			var semaphore = new SemaphoreSlim(3); 

			var tasks = pendingFiles.Select(async file =>
			{
				await semaphore.WaitAsync(stoppingToken);

				try
				{
					List<EmailInvitationRequest> subjects = new();

					await using var stream =
						await uploadFiles.DownloadAsync(file.FileKey!, stoppingToken);

					using var workbook = new XLWorkbook(stream);
					var worksheet = workbook.Worksheet(1);

					foreach (var row in worksheet.RowsUsed().Skip(1))
					{
						var token = _secureToken.GenerateSecureToken();

						if (string.IsNullOrEmpty(token))
						{
							_logger.LogError("Failed Transaction: Failed to generate Token for identity: {@Context}");
							throw new InternalServerException("Failed to generate Token.");
						}

						var HashToken = _hashService.Hash(token);

						if (string.IsNullOrEmpty(HashToken))
						{
							_logger.LogError("Failed Transaction: Failed to hash Token for identity: {@Context}");
							throw new InternalServerException("Failed to hash Token.");
						}

						subjects.Add(new EmailInvitationRequest
						{
							EmailInvitationID = Guid.CreateVersion7(),
							HashToken = HashToken,
							HashTokenCreated = DateTime.UtcNow,
							HashTokenExpiration = DateTime.UtcNow.AddHours(_applicationFormExpiryInHours),
							LastName = row.Cell(1).GetString(),
							FirstName = row.Cell(2).GetString(),
							MiddleInitial = row.Cell(3).GetString(),
							EmailAddress = row.Cell(4).GetString(),
							MobileNumber = row.Cell(5).GetString(),
							SelectPackage = row.Cell(6).GetString(),
							RushNormal = row.Cell(7).GetString()
						});
					}

					await repository.UpdateBulkEmailInvitationRequestAsync(subjects);
					return subjects;
				}
				finally
				{
					semaphore.Release();
				}
			});

			List<EmailInvitationRequest>[] results = await Task.WhenAll(tasks);

			var listOfListOfSubjects = results.ToList();

			await _hybridCache.SetAsync(
					"BulkUpload_Subjects",
					listOfListOfSubjects,
					new HybridCacheEntryOptions
					{
						Expiration = TimeSpan.FromMinutes(10)
					});


			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}
	}
}