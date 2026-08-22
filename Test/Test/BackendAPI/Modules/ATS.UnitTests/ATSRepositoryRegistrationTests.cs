using ATS.Data.Cache;
using ATS.Data.Context;
using ATS.Data.Repository;
using ATS.ServiceConfig;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class ATSRepositoryRegistrationTests
{
	[Fact]
	public void AddATSServices_ShouldDecorateAggregateAndForwardFocusedInterfaces()
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

		var aggregate = scope.ServiceProvider.GetRequiredService<IATSRepository>();
		aggregate.Should().BeOfType<ATSCacheRepository>();

		var repositoryField = typeof(ATSCacheRepository).GetField(
			"_atsRepository",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		repositoryField.Should().NotBeNull();
		repositoryField!.GetValue(aggregate).Should().BeOfType<ATSRepository>();

		AssertForwardsToAggregate<IApplicantSearchProjectionRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IApplicationFormRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IATSUserRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IBulkUploadRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IClientRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IDashboardRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IDisputeOrderRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IEmailInvitationRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IModuleRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IOrderHistoryRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IPackageRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IReportRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IRoleRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IUserClientRepository>(scope.ServiceProvider, aggregate);
		AssertForwardsToAggregate<IWithdrawnApplicationRepository>(scope.ServiceProvider, aggregate);
	}

	private static void AssertForwardsToAggregate<TService>(IServiceProvider provider, IATSRepository aggregate)
		where TService : class
	{
		provider.GetRequiredService<TService>().Should().BeSameAs(aggregate);
	}
}
