using ATS.Configuration;
using ATS.Constants;
using ATS.Data.Repository;
using ATS.Hubs;
using ATS.Services.BulkSubmissionProcessor;
using ATS.Services.EmailAccounts;
using ATS.Services.EmailNotificationProcessor;
using ATS.Services.EmailService;
using ATS.Services.EndorsementSubmission;
using ATS.Services.Notifications;
using ATS.Services.OrderHistory;
using Auth.Shared.Contracts;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

public class ATSServiceFixture : IDisposable
{
	// Common mocks
	public Mock<IATSRepository> MockRepository { get; private set; }
	public Mock<IEndorsementSubmissionService> MockEndorsementSubmissionService { get; private set; }
	public Mock<IObjectStorageService> MockObjectStorage { get; private set; }
	public Mock<ISecureToken> MockSecureToken { get; private set; }
	public Mock<IHashService> MockHashService { get; private set; }
	public Mock<IHubContext<ATSHub, IATSClient>> MockHubContext { get; private set; }
	public Mock<IHubClients<IATSClient>> MockClients { get; private set; }
	public Mock<IATSClient> MockATSClient { get; private set; }
	public Mock<IServiceScopeFactory> MockServiceScopeFactory { get; private set; }
	public Mock<ICurrentUser> MockCurrentUser { get; private set; }
	public Mock<IOrderHistoryService> MockOrderHistoryService { get; private set; }
	public Mock<IAtsNotificationService> MockNotificationService { get; private set; }

	// Loggers
	public Mock<ILogger<BulkSubmissionProcessorService>> MockBulkSubmissionProcessorServiceLogger { get; private set; }
	public Mock<ILogger<EmailNotificationProcessorService>> EmailNotificationProcessoServiceLogger { get; private set; }

	// Configuration
	public IConfiguration Configuration { get; private set; }
	public AtsEmailDeliveryOptions EmailDeliveryOptions { get; private set; }

	/// <summary>
	/// Stands in for the per-account pools. Defaults to one healthy account, so a test that
	/// does not care about failover behaves as it did when there was a single hard-coded
	/// sender; tests that do care re-Setup GetNextSendableAccountAsync themselves.
	/// </summary>
	public Mock<ISmtpAccountPoolRegistry> MockPoolRegistry { get; private set; }

	// Service instances
	public BulkSubmissionProcessorService BulkSubmissionProcessorService { get; private set; }
	public EmailNotificationProcessorService EmailNotificationProcessorService { get; private set; }

	public ATSServiceFixture()
	{
		// init mocks
		MockRepository = new Mock<IATSRepository>();
		MockEndorsementSubmissionService = new Mock<IEndorsementSubmissionService>();
		MockObjectStorage = new Mock<IObjectStorageService>();
		MockSecureToken = new Mock<ISecureToken>();
		MockHashService = new Mock<IHashService>();
		MockHubContext = new Mock<IHubContext<ATSHub, IATSClient>>();
		MockClients = new Mock<IHubClients<IATSClient>>();
		MockATSClient = new Mock<IATSClient>();
		MockServiceScopeFactory = new Mock<IServiceScopeFactory>();
		MockCurrentUser = new Mock<ICurrentUser>();
		MockOrderHistoryService = new Mock<IOrderHistoryService>();
		MockNotificationService = new Mock<IAtsNotificationService>();

		MockBulkSubmissionProcessorServiceLogger = new();
		EmailNotificationProcessoServiceLogger = new();

		// configuration values required by several services
		Configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "ATS:ATSApplicationFormExpiryInHours", "24" },
				{ "ATS:ApplicationFormBaseUrl", "https://example.com/form" }
			})
			.Build();

		MockHubContext
			.Setup(x => x.Clients)
			.Returns(MockClients.Object);

		MockClients
			.Setup(x => x.Group(It.IsAny<string>()))
			.Returns(MockATSClient.Object);

		SetupServiceScopeFactory();

		BulkSubmissionProcessorService = new BulkSubmissionProcessorService(
			MockRepository.Object,
			MockServiceScopeFactory.Object,
			MockObjectStorage.Object,
			MockSecureToken.Object,
			MockHashService.Object,
			MockHubContext.Object,
			MockBulkSubmissionProcessorServiceLogger.Object,
			Configuration);

		// Fast on purpose. The production defaults pace sends at 0.9/s to stay under the
		// provider's limit; a test asserting on three rows must not wait three seconds for
		// them, and the limiter's behaviour is covered directly by its own tests.
		EmailDeliveryOptions = new AtsEmailDeliveryOptions
		{
			MaxSendsPerSecond = 10_000,
			MaxAttemptsPerPass = 3,
			RetryBaseDelaySeconds = 0,
			ThrottleBackoffSeconds = 600
		};

		// Somebody to address the "every sender account is down" notification to. Without this
		// the mock returns null, the processor's guard swallows the NullReferenceException, and
		// the outage notification silently never fires - which is the exact bug the
		// notification exists to prevent, hidden inside a green test.
		MockRepository
			.Setup(x => x.GetAtsAdministratorUserIdsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<Guid> { AdministratorUserId });

		MockPoolRegistry = new Mock<ISmtpAccountPoolRegistry>();

		// One healthy account available by default. The processor now asks before claiming a
		// slice of rows, so without this every pass would short-circuit as "no account
		// available" and the existing tests would assert against a pass that never ran.
		MockPoolRegistry
			.Setup(x => x.GetNextSendableAccountAsync(
				It.IsAny<IReadOnlyCollection<int>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(HealthyAccount);

		// IEndorsementSubmissionService is no longer injected: each send resolves its own
		// from a scope, because it reaches a DbContext and the sends now run concurrently.
		// MockEndorsementSubmissionService is registered on the scope factory instead.
		EmailNotificationProcessorService = new EmailNotificationProcessorService(
			EmailNotificationProcessoServiceLogger.Object,
			MockRepository.Object,
			MockNotificationService.Object,
			MockServiceScopeFactory.Object,
			Configuration,
			MockPoolRegistry.Object,
			Options.Create(EmailDeliveryOptions)
			);
	}

	/// <summary>The admin the exhausted-accounts notification is addressed to.</summary>
	public static Guid AdministratorUserId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

	/// <summary>A verified, active, uncapped account, for tests that do not exercise failover.</summary>
	public static AtsEmailAccountSnapshot HealthyAccount { get; } = new(
		AtsEmailAccountId: 1,
		DisplayName: "Applicant Tracking System",
		EmailAddress: "ats@example.com",
		SmtpHost: "smtp.example.com",
		SmtpPort: 587,
		Priority: 1,
		IsActive: true,
		DailySendLimit: 450,
		VerificationStatus: AtsEmailAccountStatus.Verified,
		VerifiedAt: DateTime.UtcNow.AddDays(-1),
		ConsecutiveFailureCount: 0,
		CoolingDownUntil: null,
		LastFailureReason: null,
		LastSentAt: null,
		ConsumedInWindow: 0);

	public void Dispose()
	{
	}

	private void SetupServiceScopeFactory()
	{
		var mockServiceScope = new Mock<IServiceScope>();
		var mockServiceProvider = new Mock<IServiceProvider>();

		mockServiceProvider
			.Setup(x => x.GetService(typeof(IATSRepository)))
			.Returns(MockRepository.Object);

		// The bulk parsing job resolves this per file to record an OrderCreated entry
		// for every order it creates.
		mockServiceProvider
			.Setup(x => x.GetService(typeof(IOrderHistoryService)))
			.Returns(MockOrderHistoryService.Object);

		// Resolved per file to raise the "bulk upload processed" notification alongside
		// the existing SignalR toast. Without this the job throws on GetRequiredService
		// and never reaches the status update the tests assert on.
		mockServiceProvider
			.Setup(x => x.GetService(typeof(IAtsNotificationService)))
			.Returns(MockNotificationService.Object);

		// The email processor resolves one of these per invitation rather than sharing the
		// injected instance, because the sends run concurrently and it reaches a DbContext.
		mockServiceProvider
			.Setup(x => x.GetService(typeof(IEndorsementSubmissionService)))
			.Returns(MockEndorsementSubmissionService.Object);

		mockServiceScope
			.Setup(x => x.ServiceProvider)
			.Returns(mockServiceProvider.Object);

		MockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);
	}
}
