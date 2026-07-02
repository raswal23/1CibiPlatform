using ATS.Data.Context;
using ATS.Services;
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
	protected readonly IHttpContextAccessor _httpContextAccessor;
	protected readonly IConfiguration _configuration;
	protected readonly ISecureToken _generateToken;
	protected readonly IObjectStorageService _objectStorageService;
	protected readonly IEndorsementSubmissionService _endorsementSubmissionService;
	protected readonly IEmailNotificationProcessorService _emailNotificationProcessorService;
	protected readonly IBulkSubmissionProcessorService _bulkSubmissionProcessorService;
	protected readonly HybridCache _hybridCache;
	protected readonly IConnectionMultiplexer _redis;

	protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
	{
		_scope = factory.Services.CreateScope();
		_sender = _scope.ServiceProvider.GetRequiredService<ISender>();
		_hashService = _scope.ServiceProvider.GetRequiredService<IHashService>();
		_generateToken = _scope.ServiceProvider.GetRequiredService<ISecureToken>();
		_dbContext = _scope.ServiceProvider.GetRequiredService<ATSDBContext>();
		_hybridCache = _scope.ServiceProvider.GetRequiredService<HybridCache>();
		_redis = _scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
		_httpContextAccessor = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
		_configuration = _scope.ServiceProvider.GetRequiredService<IConfiguration>();
		_objectStorageService = _scope.ServiceProvider.GetRequiredService<IObjectStorageService>();
		_endorsementSubmissionService = _scope.ServiceProvider.GetRequiredService<IEndorsementSubmissionService>();
		_emailNotificationProcessorService = _scope.ServiceProvider.GetRequiredService<IEmailNotificationProcessorService>();
		_bulkSubmissionProcessorService = _scope.ServiceProvider.GetRequiredService<IBulkSubmissionProcessorService>();
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
								ats.""EmailInvitationRequest"", 
								ats.""LicensesDetails"", 
								ats.""PersonalDetails"", 
								ats.""ProfessionalExperiences"", 
								ats.""ReferenceDetails"",
								ats.""SignatureDetails""
						  RESTART IDENTITY CASCADE;";
				await _dbContext.Database.ExecuteSqlRawAsync(sql);
			}
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
