using Auth.Data.Repository;
using Auth.Service;
using Auth.Services;
using Auth.Shared.Contracts;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.Auth.UnitTests.Fixture
{
	public class AuthServiceFixture : IDisposable
	{
		// Common mocks
		public Mock<IEmailService> MockEmailService { get; private set; }
		public Mock<IPasswordHasherService> MockPasswordHasherService { get; private set; }
		public Mock<IAuthRepository> MockAuthRepository { get; private set; }
		public Mock<IHashService> MockHashService { get; private set; }
		public Mock<IOtpService> MockOtpService { get; private set; }
		public Mock<ISecureToken> MockSecureToken { get; private set; }
		public Mock<IJWTService> MockJwtService { get; private set; }
		public Mock<IRefreshTokenService> MockRefreshTokenService { get; private set; }
		public Mock<IAtsAccessClaimsProvider> MockAtsAccessClaimsProvider { get; private set; }
		public Mock<IHttpContextAccessor> MockHttpContextAccessor { get; private set; }
		public Mock<HybridCache> MockHybridCache { get; private set; }
		// Loggers
		public Mock<ILogger<RegisterService>> MockRegisterLogger { get; private set; }
		public Mock<ILogger<LoginService>> MockLoginLogger { get; private set; }
		public Mock<ILogger<RefreshTokenService>> MockRefreshLogger { get; private set; }
		public Mock<ILogger<ForgotPasswordService>> MockForgotLogger { get; private set; }
		public Mock<ILogger<UserService>> MockUserManagementLogger { get; private set; }
		public Mock<ILogger<ApplicationService>> MockApplicationLogger { get; private set; }
		public Mock<ILogger<SubMenuService>> MockSubMenuLogger { get; private set; }
		public Mock<ILogger<AppSubRoleService>> MockAppSubRoleLogger { get; private set; }
		public Mock<ILogger<RoleService>> MockRoleLogger { get; private set; }
		public Mock<ILogger<LockedUserService>> MockLockedUserLogger { get; private set; }
		// Configuration
		public IConfiguration Configuration { get; private set; }

		// Service instances
		public RegisterService RegisterService { get; private set; }
		public LoginService LoginService { get; private set; }
		public RefreshTokenService RefreshTokenService { get; private set; }
		public ForgotPasswordService ForgotPasswordService { get; private set; }
		public JWTService JwtService { get; private set; }
		public UserService UserManagementService { get; private set; }
		public ApplicationService ApplicationService { get; private set; }
		public SubMenuService SubMenuService { get; private set; }
		public AppSubRoleService AppSubRoleService { get; private set; }
		public RoleService RoleService { get; private set; }
		public LockedUserService LockedUserService { get; private set; }
		public AuthServiceFixture()
		{
			// init mocks
			MockEmailService = new Mock<IEmailService>();
			MockPasswordHasherService = new Mock<IPasswordHasherService>();
			MockAuthRepository = new Mock<IAuthRepository>();
			MockHashService = new Mock<IHashService>();
			MockOtpService = new Mock<IOtpService>();
			MockSecureToken = new Mock<ISecureToken>();
			MockJwtService = new Mock<IJWTService>();
			MockRefreshTokenService = new Mock<IRefreshTokenService>();
			MockAtsAccessClaimsProvider = new Mock<IAtsAccessClaimsProvider>();
			MockAtsAccessClaimsProvider
				.Setup(x => x.GetClaimsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((AtsAccessClaims?)null);
			MockHttpContextAccessor = new Mock<IHttpContextAccessor>();
			MockHybridCache = new Mock<HybridCache>();

			MockRegisterLogger = new Mock<ILogger<RegisterService>>();
			MockLoginLogger = new Mock<ILogger<LoginService>>();
			MockRefreshLogger = new Mock<ILogger<RefreshTokenService>>();
			MockForgotLogger = new Mock<ILogger<ForgotPasswordService>>();
			MockUserManagementLogger = new Mock<ILogger<UserService>>();
			MockApplicationLogger = new Mock<ILogger<ApplicationService>>();
			MockSubMenuLogger = new Mock<ILogger<SubMenuService>>();
			MockAppSubRoleLogger = new Mock<ILogger<AppSubRoleService>>();
			MockRoleLogger = new Mock<ILogger<RoleService>>();
			MockLockedUserLogger = new Mock<ILogger<LockedUserService>>();

			// configuration values required by several services
			Configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new[] {
					new KeyValuePair<string,string>("Email:OtpExpirationInMinutes","10"),
					new KeyValuePair<string,string>("FrontEndMetadata:ForgotPasswordUrl","https://app.local"),
					new KeyValuePair<string,string>("Email:PasswordTokenExpirationInMinutes","60"),
					new KeyValuePair<string,string>("HttpCookieOnlyKey","cookieKey"),
					new KeyValuePair<string,string>("Jwt:ExpiryInMinutes","60"),
					new KeyValuePair<string,string>("AuthWeb:AuthWebHttpCookieOnlyKey","refreshKey"),
					new KeyValuePair<string,string>("AuthWeb:CookieExpiryInDayIsRememberMe","7"),
					new KeyValuePair<string,string>("AuthWeb:isHttps","false"),
					new KeyValuePair<string,string>("AuthWeb:AccountLockDurationInMinutes","60"),
					new KeyValuePair<string,string>("AuthWeb:MaxFailedAttemptsBeforeLockout","4")
				})
				.Build();

			// setup http context for cookie operations
			var ctx = new DefaultHttpContext();
			MockHttpContextAccessor.SetupGet(x => x.HttpContext).Returns(ctx);

			// create service instances using shared mocks
			RegisterService = new RegisterService(
				MockEmailService.Object,
				MockPasswordHasherService.Object,
				MockAuthRepository.Object,
				MockHashService.Object,
				MockOtpService.Object,
				Configuration,
				MockRegisterLogger.Object);

			LoginService = new LoginService(
				MockAuthRepository.Object,
				MockPasswordHasherService.Object,
				Configuration,
				MockJwtService.Object,
				MockRefreshTokenService.Object,
				MockAtsAccessClaimsProvider.Object,
				MockHttpContextAccessor.Object,
				MockHybridCache!.Object,
				MockLoginLogger.Object);

			RefreshTokenService = new RefreshTokenService(
				MockAuthRepository.Object,
				MockHttpContextAccessor.Object,
				MockJwtService.Object,
				MockAtsAccessClaimsProvider.Object,
				Configuration,
				MockRefreshLogger.Object);

			ForgotPasswordService = new ForgotPasswordService(
				MockAuthRepository.Object,
				MockForgotLogger.Object,
				MockEmailService.Object,
				Configuration,
				MockSecureToken.Object,
				MockHashService.Object,
				MockPasswordHasherService.Object);

			UserManagementService = new UserService(
				MockAuthRepository.Object,
				MockEmailService.Object,
				MockUserManagementLogger.Object);

			ApplicationService = new ApplicationService(
				MockAuthRepository.Object,
				MockApplicationLogger.Object
				);

			SubMenuService = new SubMenuService(
				MockAuthRepository.Object,
				MockSubMenuLogger.Object
				);

			AppSubRoleService = new AppSubRoleService(
				MockAuthRepository.Object,
				MockEmailService.Object,
				MockAppSubRoleLogger.Object
				);

			RoleService = new RoleService(
				MockAuthRepository.Object,
				MockRoleLogger.Object
				);

			LockedUserService = new LockedUserService(
				MockAuthRepository.Object,
				MockLockedUserLogger.Object
				)
;
			JwtService = new JWTService(Configuration);
		}

		public void Dispose()
		{
			// nothing to dispose currently
		}
	}
}
