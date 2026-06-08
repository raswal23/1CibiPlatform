using BuildingBlocks.Exceptions;
using FluentAssertions;
using PhilSys.Data.Entities;
using PhilSys.Features.DeleteTransaction;
using Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;

namespace Test.BackendAPI.Modules.PhilSys.IntegrationTests;

public class DeleteTransactionIntegrationTests : BaseIntegrationTest
{
	public DeleteTransactionIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
	}

	[Fact]
	public async Task DeleteTransaction_ShouldThrow_WhenTransactionDataNotFound()
	{

		// Arrange
		var token = "hash-token";
		
		var command = new DeleteTransactionCommand(token);

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		//Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("Transaction record not found.");
	}

	[Fact]
        public async Task DeleteTransaction_ShouldReturnTrue_WhenSuccessful()
        {
            // Arrange
            var transaction = new PhilSysTransaction
            {
                Tid = Guid.CreateVersion7(),
                InquiryType = "pcn",
                PCN = "9700018324631576",
                WebHookUrl = "/",
                IsTransacted = false,
                HashToken = "HabaL5avPCryiszlRKNU7Q9xClqKEq5h2FWNLdMNEpo",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PhilSysTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();

            var command = new DeleteTransactionCommand(transaction.HashToken);

            // Act
            var result = await _sender.Send(command);

            // Assert
            result.Should().NotBeNull();
            result.IsDeleted.Should().BeTrue();
        }
}
