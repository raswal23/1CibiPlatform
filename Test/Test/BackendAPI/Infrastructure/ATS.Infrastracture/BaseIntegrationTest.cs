using ATS.Data.Context;
using ATS.Data.Repository;
using ATS.Services;
using Auth.Data.Context;
using Auth.Shared.Contracts;
using BuildingBlocks.SharedServices.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Test.BackendAPI.Infrastructure.ATS.Infrastracture;

public class BaseIntegrationTest : IClassFixture<IntegrationTestWebAppFactory>, IAsyncLifetime
{
	private readonly IServiceScope _scope;
	protected readonly ISender _sender;
	protected readonly IHashService _hashService;
	protected readonly ATSDBContext _dbContext;
	protected readonly AuthApplicationDbContext _authDbContext;
	protected readonly IHttpContextAccessor _httpContextAccessor;
	protected readonly IConfiguration _configuration;
	protected readonly ISecureToken _generateToken;
	protected readonly IObjectStorageService _objectStorageService;
	protected readonly IEndorsementSubmissionService _endorsementSubmissionService;
	protected readonly IEmailNotificationProcessorService _emailNotificationProcessorService;
	protected readonly IBulkSubmissionProcessorService _bulkSubmissionProcessorService;
	protected readonly IPackageManagementService _packageManagementService;
	protected readonly IRoleManagementService _roleManagementService;
	protected readonly IModuleManagementService _moduleManagementService;
	protected readonly IClientManagementService _clientManagementService;
	protected readonly IUserManagementService _userManagementService;
	protected readonly IClientAssignmentService _clientAssignmentService;
	protected readonly IAtsAccessClaimsProvider _atsAccessClaimsProvider;
	protected readonly IApplicantSearchProjectionService _applicantSearchProjectionService;
	protected readonly IReportService _reportService;
	protected readonly IATSRepository _atsRepository;
	protected readonly HybridCache _hybridCache;
	protected readonly IConnectionMultiplexer _redis;

	protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
	{
		_scope = factory.Services.CreateScope();
		_sender = _scope.ServiceProvider.GetRequiredService<ISender>();
		_hashService = _scope.ServiceProvider.GetRequiredService<IHashService>();
		_generateToken = _scope.ServiceProvider.GetRequiredService<ISecureToken>();
		_dbContext = _scope.ServiceProvider.GetRequiredService<ATSDBContext>();
		_authDbContext = _scope.ServiceProvider.GetRequiredService<AuthApplicationDbContext>();
		_hybridCache = _scope.ServiceProvider.GetRequiredService<HybridCache>();
		_redis = _scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
		_httpContextAccessor = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
		_configuration = _scope.ServiceProvider.GetRequiredService<IConfiguration>();
		_objectStorageService = _scope.ServiceProvider.GetRequiredService<IObjectStorageService>();
		_endorsementSubmissionService = _scope.ServiceProvider.GetRequiredService<IEndorsementSubmissionService>();
		_emailNotificationProcessorService = _scope.ServiceProvider.GetRequiredService<IEmailNotificationProcessorService>();
		_bulkSubmissionProcessorService = _scope.ServiceProvider.GetRequiredService<IBulkSubmissionProcessorService>();
		_packageManagementService = _scope.ServiceProvider.GetRequiredService<IPackageManagementService>();
		_roleManagementService = _scope.ServiceProvider.GetRequiredService<IRoleManagementService>();
		_moduleManagementService = _scope.ServiceProvider.GetRequiredService<IModuleManagementService>();
		_clientManagementService = _scope.ServiceProvider.GetRequiredService<IClientManagementService>();
		_userManagementService = _scope.ServiceProvider.GetRequiredService<IUserManagementService>();
		_clientAssignmentService = _scope.ServiceProvider.GetRequiredService<IClientAssignmentService>();
		_atsAccessClaimsProvider = _scope.ServiceProvider.GetRequiredService<IAtsAccessClaimsProvider>();
		_applicantSearchProjectionService = _scope.ServiceProvider.GetRequiredService<IApplicantSearchProjectionService>();
		_reportService = _scope.ServiceProvider.GetRequiredService<IReportService>();
		_atsRepository = _scope.ServiceProvider.GetRequiredService<IATSRepository>();
	}


	public async Task InitializeAsync()
	{
		try
		{
			if (_dbContext is not null)
			{
				// Table is in the ats schema
				var sql = @"TRUNCATE TABLE 
								ats.""AddressDetails"", 
								ats.""DocumentDetails"", 
								ats.""EducationalBackground"", 
								ats.""ReportDetails"",
								ats.""ArchiveReport"",
								ats.""EmailInvitationRequest"", 
								ats.""LicensesDetails"", 
								ats.""PersonalDetails"", 
								ats.""ProfessionalExperiences"", 
								ats.""ReferenceDetails"",
								ats.""SignatureDetails"",
								ats.""BulkUploadFileDetails"",
								ats.""ApplicantSearchProjection"",
								ats.""UserClientDetails"",
								ats.""UserDetails"",
								ats.""ClientDetails"",
								ats.""PackageDetails"",
								ats.""RoleDetails"",
								ats.""ModuleDetails""
						  RESTART IDENTITY CASCADE;";
				await _dbContext.Database.ExecuteSqlRawAsync(sql);
			}

			if (_authDbContext is not null)
			{
				var sql = @"TRUNCATE TABLE
								""AuthUserAppRoles"",
								""AuthUsers"",
								""AuthApplications"",
								""AuthRoles""
						  RESTART IDENTITY CASCADE;";
				await _authDbContext.Database.ExecuteSqlRawAsync(sql);
			}

			await _hybridCache.RemoveByTagAsync("user");
			await _hybridCache.RemoveByTagAsync("userclient");
			await _hybridCache.RemoveByTagAsync("users");
			await _hybridCache.RemoveByTagAsync("appsubroles");
			await _hybridCache.RemoveByTagAsync("disputeorder");
			await _hybridCache.RemoveByTagAsync("report");
			await _hybridCache.RemoveByTagAsync("withdrawnapplication");

			if (_objectStorageService is MockObjectStorageService mockObjectStorage)
				mockObjectStorage.Clear();
		}
		catch (Exception ex)
		{
			throw new Exception("Error during database cleanup in InitializeAsync: " + ex.Message, ex);
		}
	}

	public Task DisposeAsync()
	{
		return Task.CompletedTask;
	}
}
