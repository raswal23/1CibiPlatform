using ATS.Data.Cache.Administration;
using ATS.Data.Context;
using ATS.Data.Repository.Administration.Clients;
using ATS.Data.Repository.Administration.Modules;
using ATS.Data.Repository.Administration.PackageManagement;
using ATS.Data.Repository.Administration.Roles;
using ATS.Data.Repository.Administration.Users;
using ATS.ServiceConfig;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class ATSRepositoryRegistrationTests
{
	[Fact]
	public void AddATSServices_ShouldDecorateEachAdministrationRepository()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddHybridCache();
		services.AddDbContext<ATSDBContext>(options => options.UseNpgsql(
			"Host=localhost;Database=registration_test;Username=test;Password=test"));
		services.AddATSServices();

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true
		});
		using var scope = provider.CreateScope();

		AssertDecorator<IPackageRepository, PackageCacheRepository, PackageRepository>(scope.ServiceProvider);
		AssertDecorator<IClientRepository, ClientCacheRepository, ClientRepository>(scope.ServiceProvider);
		AssertDecorator<IRoleRepository, RoleCacheRepository, RoleRepository>(scope.ServiceProvider);
		AssertDecorator<IModuleRepository, ModuleCacheRepository, ModuleRepository>(scope.ServiceProvider);
		AssertDecorator<IATSUserRepository, ATSUserCacheRepository, ATSUserRepository>(scope.ServiceProvider);
	}

	private static void AssertDecorator<TService, TDecorator, TImplementation>(IServiceProvider provider)
		where TService : class
		where TDecorator : class, TService
		where TImplementation : class, TService
	{
		var service = provider.GetRequiredService<TService>();
		service.Should().BeOfType<TDecorator>();

		var repositoryField = typeof(TDecorator).GetField(
			"_repository",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		repositoryField.Should().NotBeNull();
		repositoryField!.GetValue(service).Should().BeOfType<TImplementation>();
	}
}
