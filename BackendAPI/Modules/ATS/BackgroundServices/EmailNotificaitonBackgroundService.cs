using Microsoft.Extensions.Caching.Hybrid;

namespace ATS.BackgroundServices;

public class EmailNotificaitonBackgroundService : BackgroundService
{
	private readonly ILogger<EmailNotificaitonBackgroundService> _logger;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IConfiguration _configuration;
	private readonly HybridCache _hybridCache;
	private readonly string _applicationformBaseUrl;

	public EmailNotificaitonBackgroundService(
		ILogger<EmailNotificaitonBackgroundService> logger,
		IServiceScopeFactory scopeFactory, 
		IConfiguration configuration,
		HybridCache hybridCache)
	{
		_logger = logger;
		_scopeFactory = scopeFactory;
		_configuration = configuration;
		_applicationformBaseUrl = _configuration.GetSection("ATS").GetValue<string>("ApplicationFormBaseUrl") ?? string.Empty;
		_hybridCache = hybridCache;
	}
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			using var scope = _scopeFactory.CreateScope();

			var sendEmail = scope.ServiceProvider
				.GetRequiredService<IEndorsementSubmissionService>();

			var repository = scope.ServiceProvider
				.GetRequiredService<IATSRepository>();

			var cached = await _hybridCache.GetOrCreateAsync(
				"BulkUpload_Subjects",
				async entry =>
				{
					return new List<List<EmailInvitationRequest>>();
				});

			var allRequests = cached.SelectMany(x => x).ToList();

			List<EmailInvitationRequest> successList = new();
			List<EmailInvitationRequest> errorList = new();

			// Initial processing
			foreach (var request in allRequests)
			{
				try
				{
					if (string.IsNullOrWhiteSpace(request.EmailAddress))
					{
						errorList.Add(request);
						continue;
					}

					var subjectName = $"{request.FirstName} {request.LastName}";

					await sendEmail.SendApplicationFormToUserEmailAsync(
						request.EmailAddress,
						subjectName,
						_applicationformBaseUrl);

					successList.Add(request);
				}
				catch (Exception ex)
				{
					_logger.LogError(
						ex,
						"Failed to send email to {Email}",
						request.EmailAddress);

					errorList.Add(request);
				}
			}

			// Retry failed emails
			const int maxRetries = 3;

			for (int retry = 1; retry <= maxRetries && errorList.Any(); retry++)
			{
				_logger.LogInformation(
					"Retry round {Retry}. Failed emails count: {Count}",
					retry,
					errorList.Count);

				var failedItems = errorList.ToList();

				errorList.Clear();

				foreach (var request in failedItems)
				{
					try
					{
						if (string.IsNullOrWhiteSpace(request.EmailAddress))
						{
							errorList.Add(request);
							continue;
						}

						var subjectName = $"{request.FirstName} {request.LastName}";

						await sendEmail.SendApplicationFormToUserEmailAsync(
							request.EmailAddress,
							subjectName,
							_applicationformBaseUrl);

						successList.Add(request);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(
							ex,
							"Retry {Retry} failed for {Email}",
							retry,
							request.EmailAddress);

						errorList.Add(request);
					}
				}

				// Optional delay before next retry round
				if (errorList.Any())
				{
					await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
				}
			}

			_logger.LogInformation(
				"Email processing completed. Success: {SuccessCount}, Failed: {FailedCount}",
				successList.Count,
				errorList.Count);

			// - Update DB status for successList
			await repository.UpdateEmailInvitationRequestForSuccessAsync(successList);

			// - Update DB status for errorList
			await repository.UpdateEmailInvitationRequestForErrorAsync(successList);

			// - Remove successfully processed items from cache if needed
			await _hybridCache.RemoveAsync("BulkUpload_Subjects");

			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}
	}
}

