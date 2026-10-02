using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhilSys.Data.Entities;
using PhilSys.Services;
using Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;

namespace Test.BackendAPI.Modules.PhilSys.IntegrationTests;

public class TransactionCleanupIntegrationTests : BaseIntegrationTest
{
	private readonly IntegrationTestWebAppFactory _factory;

	public TransactionCleanupIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_factory = factory;
	}

	private static PhilSysTransaction CreateTransaction(string hashToken, bool isTransacted, DateTime createdAt) => new PhilSysTransaction
	{
		Tid = Guid.CreateVersion7(),
		InquiryType = "pcn",
		PCN = "6786785465456459",
		HashToken = hashToken,
		WebHookUrl = "/",
		IsTransacted = isTransacted,
		TransactedAt = isTransacted ? createdAt.AddMinutes(2) : null,
		CreatedAt = createdAt,
		ExpiresAt = createdAt.AddMinutes(5)
	};

	[Fact]
	public async Task CleanupExpiredTransactions_ShouldDeleteOnlyStaleUntransactedRows()
	{
		// Arrange - the Quartz hosted service is removed in tests, so the cleanup
		// service is invoked directly instead of waiting for the trigger.
		var staleUntransacted = CreateTransaction("stale-untransacted", isTransacted: false, DateTime.UtcNow.AddMinutes(-10));
		var freshUntransacted = CreateTransaction("fresh-untransacted", isTransacted: false, DateTime.UtcNow);
		var staleTransacted = CreateTransaction("stale-transacted", isTransacted: true, DateTime.UtcNow.AddMinutes(-10));

		_dbContext.PhilSysTransactions.AddRange(staleUntransacted, freshUntransacted, staleTransacted);
		await _dbContext.SaveChangesAsync();

		// Act
		using (var scope = _factory.Services.CreateScope())
		{
			var cleanupService = scope.ServiceProvider.GetRequiredService<ITransactionCleanupService>();
			await cleanupService.CleanupExpiredTransactionsAsync();
		}

		// Assert
		var remaining = await _dbContext.PhilSysTransactions
			.AsNoTracking()
			.Select(t => t.HashToken)
			.ToListAsync();

		remaining.Should().NotContain("stale-untransacted");
		remaining.Should().Contain("fresh-untransacted");
		remaining.Should().Contain("stale-transacted");
	}
}
