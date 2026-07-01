using ATS.Data.Repository;
using ATS.Hubs;
using ATS.Services;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

public class ATSServiceFixture : IDisposable
{
	// Common mocks
	public Mock<IATSRepository> MockRepository { get; private set; }
	public Mock<IEndorsementSubmissionService> MockEndorsementSubmissionService { get; private set; }
	public Mock<IObjectStorageService> MockObjectStorage { get; private set; }
	public Mock<ISecureToken> MockSecureToken { get; private set; }
	public Mock<IHashService> MockHashService { get; private set; }
	public Mock<IConnectionMultiplexer> MockRedis { get; private set; }
	public Mock<IDatabase> MockRedisDatabase { get; private set; }
	public Mock<HybridCache> MockHybridCache { get; private set; }
	public Mock<IHubContext<ATSHub, IATSClient>> MockHubContext { get; private set; }
	public Mock<IHubClients<IATSClient>> MockClients { get; private set; }
	public Mock<IATSClient> MockATSClient { get; private set; }
	public Mock<IServiceScopeFactory> MockServiceScopeFactory { get; private set; }

	// Loggers
	public Mock<ILogger<BulkSubmissionProcessorService>> MockBulkSubmissionProcessorServiceLogger { get; private set; }
	public Mock<ILogger<EmailNotificationProcessorService>> EmailNotificationProcessoServiceLogger { get; private set; }

	// Configuration
	public IConfiguration Configuration { get; private set; }

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
		MockRedis = new Mock<IConnectionMultiplexer>();
		MockRedisDatabase = new Mock<IDatabase>();
		MockHybridCache = new Mock<HybridCache>();
		MockHubContext = new Mock<IHubContext<ATSHub, IATSClient>>();
		MockClients = new Mock<IHubClients<IATSClient>>();
		MockATSClient = new Mock<IATSClient>();
		MockServiceScopeFactory = new Mock<IServiceScopeFactory>();

		MockBulkSubmissionProcessorServiceLogger = new();
		EmailNotificationProcessoServiceLogger = new();

		// configuration values required by several services
		Configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "ATS:ATSApplicationFormExpiryInHours", "24" },
				{ "ATS:ApplicationFormBaseUrl", "https://example.com/form" },
				{ "CacheKeys:ATSBatchesPending", "ats-batches-pending" },
				{ "CacheKeys:ATSBatchesError", "ats-batches-error" }
			})
			.Build();

		MockRedis
			.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
			.Returns(MockRedisDatabase.Object);

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
			MockHybridCache.Object,
			MockRedis.Object,
			MockHubContext.Object,
			MockBulkSubmissionProcessorServiceLogger.Object,
			Configuration);

		EmailNotificationProcessorService = new EmailNotificationProcessorService(
			EmailNotificationProcessoServiceLogger.Object,
			MockEndorsementSubmissionService.Object,
			MockRepository.Object,
			Configuration,
			MockRedis.Object,
			MockHybridCache.Object
			);
	}

	public void Dispose()
	{
		// nothing to dispose currently
	}

	private void SetupServiceScopeFactory()
	{
		var mockServiceScope = new Mock<IServiceScope>();
		var mockServiceProvider = new Mock<IServiceProvider>();

		mockServiceProvider
			.Setup(x => x.GetService(typeof(IATSRepository)))
			.Returns(MockRepository.Object);

		mockServiceScope
			.Setup(x => x.ServiceProvider)
			.Returns(mockServiceProvider.Object);

		MockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);
	}
}
