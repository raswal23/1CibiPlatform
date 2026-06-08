using Moq;
using FluentAssertions;
using Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture;
using PhilSys.Data.Entities;
using BuildingBlocks.Exceptions;
using System.Threading.Tasks;

namespace Test.BackendAPI.Modules.Philys.UnitTests
{
	public class DeleteTransactionServiceTests : IClassFixture<PhilSysServiceFixture>
	{
		private readonly PhilSysServiceFixture _fixture;

		public DeleteTransactionServiceTests(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}

		[Fact]
		public async Task DeleteTransactionAsync_ShouldThrow_WhenTransactionNotFound()
		{
			// Arrange
			var service = _fixture.DeleteTransactionService;
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(It.IsAny<string>())).ReturnsAsync((PhilSysTransaction?)null);

			// Act 
			Func<Task> act = async () => await service.DeleteTransactionAsync("PcDEy66T5oI6QVwuxNICRwBGZCzMBkr9sfKMMA40Vxs");

			// Assert
			await act.Should().ThrowAsync<NotFoundException>().WithMessage("Transaction record not found.");
		}

		[Fact]
		public async Task DeleteTransactionAsync_ShouldThrow_WhenFailedToDeleteTransaction()
		{
			// Arrange
			var service = _fixture.DeleteTransactionService;
			var transaction = new PhilSysTransaction {
				Tid = Guid.CreateVersion7(),
				InquiryType = "pcn",
				PCN = "9700018324631576",
				WebHookUrl = "/",
				IsTransacted = false,
				HashToken = "HabaL5avPCryiszlRKNU7Q9xClqKEq5h2FWNLdMNEpo",
				ExpiresAt = DateTime.UtcNow.AddMinutes(5),
				CreatedAt = DateTime.UtcNow
			};
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(transaction.HashToken)).ReturnsAsync(transaction);
			_fixture.MockPhilSysRepository.Setup(x => x.DeleteTransactionDataAsync(transaction)).ReturnsAsync(false);

			// Act
			Func<Task> act = async () => await service.DeleteTransactionAsync(transaction.HashToken);

			// Assert
			await act.Should().ThrowAsync<Exception>().WithMessage("Failed to delete the transaction record.");
		}

		[Fact]
		public async Task DeleteTransactionAsync_ShouldReturnTrue_WhenTransactionIsDeletedSuccessfully()
		{
			// Arrange
			var service = _fixture.DeleteTransactionService;
			var transactionId = Guid.CreateVersion7();
			var hashToken = "HabaL5avPCryiszlRKNU7Q9xClqKEq5h2FWNLdMNEpo";
			var transaction = new PhilSysTransaction
			{
				Tid = transactionId,
				InquiryType = "pcn",
				PCN = "9700018324631576",
				WebHookUrl = "/",
				IsTransacted = false,
				HashToken = hashToken,
				ExpiresAt = DateTime.UtcNow.AddMinutes(5),
				CreatedAt = DateTime.UtcNow
			};

			_fixture.MockPhilSysRepository
				.Setup(x => x.GetTransactionDataByHashTokenAsync(transaction.HashToken))
				.ReturnsAsync(transaction);

			_fixture.MockPhilSysRepository
				.Setup(x => x.DeleteTransactionDataAsync(transaction))
				.ReturnsAsync(true); 

			// Act
			var result = await service.DeleteTransactionAsync(transaction.HashToken);

			// Assert
			result.Should().BeTrue(); 
		}
	}
}
