using ATS.Data.Context;
using BuildingBlocks.SharedServices.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

	protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
	{
		_scope = factory.Services.CreateScope();
		_sender = _scope.ServiceProvider.GetRequiredService<ISender>();
		_hashService = _scope.ServiceProvider.GetRequiredService<IHashService>();
		_generateToken = _scope.ServiceProvider.GetRequiredService<ISecureToken>();
		_dbContext = _scope.ServiceProvider.GetRequiredService<ATSDBContext>();
		_httpContextAccessor = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
		_configuration = _scope.ServiceProvider.GetRequiredService<IConfiguration>();
		_objectStorageService = _scope.ServiceProvider.GetRequiredService<IObjectStorageService>();

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
