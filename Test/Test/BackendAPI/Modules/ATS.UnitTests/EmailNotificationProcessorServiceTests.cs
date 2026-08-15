using System.Text.Json;
using ATS.Data.Entities;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class EmailNotificationProcessorServiceTests : IClassFixture<ATSServiceFixture>
{
	private readonly ATSServiceFixture _fixture;

	public EmailNotificationProcessorServiceTests(ATSServiceFixture fixture)
	{
		_fixture = fixture;
	}

	#region Positive Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReturn_WhenNoPendingBatches()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockRedisDatabase.Invocations.Clear();
		_fixture.MockEndorsementSubmissionService.Invocations.Clear();

		// Claim script pops nothing from the pending sorted set
		_fixture.MockRedisDatabase
			.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
			.ReturnsAsync(RedisResult.Create(RedisValue.Null));

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldProcessBatch_WhenPayloadExists()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockRedisDatabase.Invocations.Clear();
		_fixture.MockEndorsementSubmissionService.Invocations.Clear();

		var batchId = $"batch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

		var request = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Juan",
			LastName = "Dela Cruz",
			EmailAddress = "juan@example.com",
			HashToken = "hashed-token"
		};

		var payload = JsonSerializer.Serialize(
			new List<List<EmailInvitationRequest>> { new() { request } });

		_fixture.MockRedisDatabase
			.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
			.ReturnsAsync(RedisResult.Create((RedisValue)batchId));

		_fixture.MockRedisDatabase
			.Setup(x => x.StringGetAsync(batchId, It.IsAny<CommandFlags>()))
			.ReturnsAsync((RedisValue)payload);

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();

		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailAsync(request.EmailAddress!, It.IsAny<string>(), It.IsAny<string>()),
			Times.Once);

		_fixture.MockRedisDatabase.Verify(
			x => x.KeyDeleteAsync((RedisKey)batchId, It.IsAny<CommandFlags>()),
			Times.Once);

		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetRemoveAsync("ats-batches-processing", (RedisValue)batchId, It.IsAny<CommandFlags>()),
			Times.Once);
	}
	#endregion

	#region Negative Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldDropBatch_WhenPayloadMissing()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockRedisDatabase.Invocations.Clear();
		_fixture.MockEndorsementSubmissionService.Invocations.Clear();

		var batchId = $"batch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

		_fixture.MockRedisDatabase
			.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
			.ReturnsAsync(RedisResult.Create((RedisValue)batchId));

		_fixture.MockRedisDatabase
			.Setup(x => x.StringGetAsync(batchId, It.IsAny<CommandFlags>()))
			.ReturnsAsync(RedisValue.Null);

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();

		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetRemoveAsync("ats-batches-processing", (RedisValue)batchId, It.IsAny<CommandFlags>()),
			Times.Once);

		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReturn_WhenRedisTimeoutOccurs()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockRedisDatabase.Invocations.Clear();

		_fixture.MockRedisDatabase
			.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
			.ThrowsAsync(new RedisTimeoutException("Redis timeout", CommandStatus.Unknown));

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}
	#endregion
}
