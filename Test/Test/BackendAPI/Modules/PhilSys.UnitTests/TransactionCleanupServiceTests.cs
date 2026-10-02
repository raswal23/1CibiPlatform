using FluentAssertions;
using Moq;
using PhilSys.Data.Entities;
using Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.PhilSys.UnitTests
{
	public class TransactionCleanupServiceTests : IClassFixture<PhilSysServiceFixture>
	{
		private readonly PhilSysServiceFixture _fixture;

		public TransactionCleanupServiceTests(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}

		private static PhilSysTransaction CreateExpiredTransaction(string hashToken) => new PhilSysTransaction
		{
			Tid = Guid.CreateVersion7(),
			InquiryType = "pcn",
			PCN = "6786785465456459",
			HashToken = hashToken,
			WebHookUrl = "/",
			IsTransacted = false,
			CreatedAt = DateTime.UtcNow.AddMinutes(-10),
			ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
		};

		[Fact]
		public async Task CleanupExpiredTransactionsAsync_ShouldDeleteEachExpiredRow()
		{
			// Arrange
			var service = _fixture.TransactionCleanupService;
			var expired = new List<PhilSysTransaction>
			{
				CreateExpiredTransaction("expired-token-1"),
				CreateExpiredTransaction("expired-token-2")
			};
			_fixture.MockPhilSysRepository.Invocations.Clear();
			_fixture.MockPhilSysRepository
				.Setup(x => x.GetExpiredUntransactedTransactionsAsync(It.IsAny<DateTime>()))
				.ReturnsAsync(expired);
			_fixture.MockPhilSysRepository
				.Setup(x => x.DeleteTransactionDataAsync(It.IsAny<PhilSysTransaction>()))
				.ReturnsAsync(true);

			// Act
			await service.CleanupExpiredTransactionsAsync();

			// Assert
			_fixture.MockPhilSysRepository.Verify(x => x.DeleteTransactionDataAsync(expired[0]), Times.Once);
			_fixture.MockPhilSysRepository.Verify(x => x.DeleteTransactionDataAsync(expired[1]), Times.Once);
		}

		[Fact]
		public async Task CleanupExpiredTransactionsAsync_ShouldNotDelete_WhenNoExpiredRows()
		{
			// Arrange
			var service = _fixture.TransactionCleanupService;
			_fixture.MockPhilSysRepository.Invocations.Clear();
			_fixture.MockPhilSysRepository
				.Setup(x => x.GetExpiredUntransactedTransactionsAsync(It.IsAny<DateTime>()))
				.ReturnsAsync(new List<PhilSysTransaction>());

			// Act
			await service.CleanupExpiredTransactionsAsync();

			// Assert
			_fixture.MockPhilSysRepository.Verify(x => x.DeleteTransactionDataAsync(It.IsAny<PhilSysTransaction>()), Times.Never);
		}

		[Fact]
		public async Task CleanupExpiredTransactionsAsync_ShouldContinue_WhenOneDeleteFails()
		{
			// Arrange
			var service = _fixture.TransactionCleanupService;
			var failing = CreateExpiredTransaction("failing-token");
			var succeeding = CreateExpiredTransaction("succeeding-token");
			_fixture.MockPhilSysRepository.Invocations.Clear();
			_fixture.MockPhilSysRepository
				.Setup(x => x.GetExpiredUntransactedTransactionsAsync(It.IsAny<DateTime>()))
				.ReturnsAsync(new List<PhilSysTransaction> { failing, succeeding });
			_fixture.MockPhilSysRepository
				.Setup(x => x.DeleteTransactionDataAsync(failing))
				.ReturnsAsync(false);
			_fixture.MockPhilSysRepository
				.Setup(x => x.DeleteTransactionDataAsync(succeeding))
				.ReturnsAsync(true);

			// Act
			await service.CleanupExpiredTransactionsAsync();

			// Assert - the failed row does not stop the sweep
			_fixture.MockPhilSysRepository.Verify(x => x.DeleteTransactionDataAsync(succeeding), Times.Once);
		}

		[Fact]
		public async Task CleanupExpiredTransactionsAsync_ShouldUseConfiguredCutoff()
		{
			// Arrange - fixture config sets PhilSys:TransactionCleanupAgeInMinutes = 6
			var service = _fixture.TransactionCleanupService;
			DateTime? capturedCutoff = null;
			_fixture.MockPhilSysRepository.Invocations.Clear();
			_fixture.MockPhilSysRepository
				.Setup(x => x.GetExpiredUntransactedTransactionsAsync(It.IsAny<DateTime>()))
				.Callback<DateTime>(cutoff => capturedCutoff = cutoff)
				.ReturnsAsync(new List<PhilSysTransaction>());

			// Act
			await service.CleanupExpiredTransactionsAsync();

			// Assert
			capturedCutoff.Should().NotBeNull();
			capturedCutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(-6), TimeSpan.FromSeconds(5));
		}
	}
}
