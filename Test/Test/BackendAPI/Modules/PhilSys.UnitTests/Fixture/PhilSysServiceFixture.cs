using PhilSys.Services;
using PhilSys.Data.Repository;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.Configuration;
using PhilSys.Data.UnitOfWork;
using ATS.Shared.Contracts;

namespace Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture
{
	public class PhilSysServiceFixture : IDisposable
	{
		// Common mocks
		public Mock<IHashService> MockHashService { get; private set; }
		public Mock<ISecureToken> MockSecureToken { get; private set; }
		public Mock<IPhilSysRepository> MockPhilSysRepository { get; private set; }
		public Mock<IHttpClientFactory> MockHttpClientFactory { get; private set; }
		public Mock<IPhilSysService> MockPhilSysService { get; private set; }
		public Mock<IUnitOfWork> MockUnitOfWork { get; private set; }
		public Mock<IATSQueries> MockATSQueries { get; private set; }

		// Loggers
		public Mock<ILogger<DeleteTransactionService>> MockDeleteTransactionLogger { get; private set; }
		public Mock<ILogger<GetLivenessKeyService>> MockGetLivenessKeyLogger { get; private set; }
		public Mock<ILogger<LivenessSessionService>> MockLivenessSessionLogger { get; private set; }
		public Mock<ILogger<PartnerSystemService>> MockPartnerSystemLogger { get; private set; }
		public Mock<ILogger<UpdateFaceLivenessSessionService>> MockUpdateFaceLivenessSessionLogger { get; private set; }
		public Mock<ILogger<PhilSysService>> MockPhilSysServiceLogger { get; private set; }

		// Configuration
		public IConfiguration Configuration { get; private set; }

		// Service instances
		public DeleteTransactionService DeleteTransactionService { get; private set; }
		public GetLivenessKeyService GetLivenessKeyService { get; private set; }
		public LivenessSessionService LivenessSessionService { get; private set; }
		public PartnerSystemService PartnerSystemService { get; private set; }
		public UpdateFaceLivenessSessionService UpdateFaceLivenessSessionService { get; private set; }
		public PhilSysService PhilSysService { get; private set; }

		public PhilSysServiceFixture()
		{
			// init mocks
			MockHashService = new Mock<IHashService>();
			MockPhilSysRepository = new Mock<IPhilSysRepository>();
			MockHttpClientFactory = new Mock<IHttpClientFactory>();
			MockPhilSysService = new Mock<IPhilSysService>();
			MockSecureToken = new Mock<ISecureToken>();
			MockUnitOfWork = new Mock<IUnitOfWork>();
			MockATSQueries = new Mock<IATSQueries>();

			MockDeleteTransactionLogger = new Mock<ILogger<DeleteTransactionService>>();
			MockGetLivenessKeyLogger = new Mock<ILogger<GetLivenessKeyService>>();
			MockLivenessSessionLogger = new Mock<ILogger<LivenessSessionService>>();
			MockPartnerSystemLogger = new Mock<ILogger<PartnerSystemService>>();
			MockUpdateFaceLivenessSessionLogger = new Mock<ILogger<UpdateFaceLivenessSessionService>>();
			MockPhilSysServiceLogger = new Mock<ILogger<PhilSysService>>();

			// configuration values required by several services
			Configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new[]
				{
					new KeyValuePair<string,string>("PhilSys:LivenessSessionExpiryInMinutes","5"),
					new KeyValuePair<string,string>("PhilSys:LivenessBaseUrl","http://localhost:5134"),
					new KeyValuePair<string,string>("PhilSys:ClientID","client-id"),
					new KeyValuePair<string,string>("PhilSys:ClientSecret","client-secret"),
					new KeyValuePair<string,string>("PhilSys:LivenessSDKPublicKey","test-key"),
					new KeyValuePair<string,string>("PhilSys:LivenessSDKPublicKeyEmpty",""),
				})
				.Build();

			// create service instances using shared mocks
			DeleteTransactionService = new DeleteTransactionService(
				MockPhilSysRepository.Object,
				new Mock<ILogger<DeleteTransactionService>>().Object
			);

			GetLivenessKeyService = new GetLivenessKeyService(
				MockGetLivenessKeyLogger.Object,
				Configuration
			);

			LivenessSessionService = new LivenessSessionService(
				MockPhilSysRepository.Object,
				MockHashService.Object,
				Configuration,
				MockLivenessSessionLogger.Object
			);

			PartnerSystemService = new PartnerSystemService(
				MockPartnerSystemLogger.Object,
				MockPhilSysRepository.Object,
				MockATSQueries.Object,
				Configuration,
				MockHashService.Object,
				MockSecureToken.Object
			);

			PhilSysService = new PhilSysService(
				MockHttpClientFactory.Object,
				MockPhilSysServiceLogger.Object
				);

			UpdateFaceLivenessSessionService = new UpdateFaceLivenessSessionService(
				MockHttpClientFactory.Object,
				MockPhilSysRepository.Object,
				MockUpdateFaceLivenessSessionLogger.Object,
				MockPhilSysService.Object,
				MockUnitOfWork.Object,
				Configuration
			);
		}
		public void Dispose()
		{
			// nothing to dispose currently
		}
	}
}
