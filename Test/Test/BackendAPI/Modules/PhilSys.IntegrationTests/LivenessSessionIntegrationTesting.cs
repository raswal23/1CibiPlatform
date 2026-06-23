using BuildingBlocks.Exceptions;
using FluentAssertions;
using PhilSys.Data.Entities;
using PhilSys.Features.IsLivenessValid;
using Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;

namespace Test.BackendAPI.Modules.PhilSys.IntegrationTests;

public class LivenessSessionIntegrationTesting : BaseIntegrationTest
{
	public LivenessSessionIntegrationTesting(IntegrationTestWebAppFactory factory) : base(factory)
	{
	}

	[Fact]
	public async Task IsLivenessUsed_ShouldThrow_WhenTransactionNotFound()
	{
		// Arrange
		var token = "non-existent-token";

		var command = new IsLivenessValidCommand(token);

		// Act
		Func<Task> act = async () => { await _sender.Send(command); };

		// Assert
		await act.Should().ThrowAsync<InternalServerException>()
			.WithMessage("There is no such transaction created for your liveness check.");
	}

	[Fact]
	public async Task IsLivenessUsed_ShouldReturnStatus_WhenTokenIsValid()
	{
		// Arrange
		var plainToken = "valid-token-123";
		var hashed = _hashService.Hash(plainToken);

		var transaction = new PhilSysTransaction
		{
			Tid = Guid.CreateVersion7(),
			InquiryType = "pcn",
			PCN = "9700018324631576",
			WebHookUrl = "/callback",
			IsTransacted = false,
			HashToken = hashed,
			ExpiresAt = DateTime.UtcNow.AddMinutes(10),
			CreatedAt = DateTime.UtcNow
		};

		_dbContext.PhilSysTransactions.Add(transaction);
		await _dbContext.SaveChangesAsync();

		var command = new IsLivenessValidCommand(transaction.HashToken);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		var status = result.TransactionStatusResponseDTO;
		status.Should().NotBeNull();
		status.WebHookURl.Should().Be(transaction.WebHookUrl);
		status.IsTransacted.Should().Be(transaction.IsTransacted);
	}
}
