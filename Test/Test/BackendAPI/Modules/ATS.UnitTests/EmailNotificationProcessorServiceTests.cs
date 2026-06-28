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

	#region ProcessForPendingStatusAsync Tests

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReturn_WhenNoPendingBatches()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		// ListLeftPopAsync returns an array of RedisValue
		_fixture.MockRedisDatabase
			.Setup(x => x.ListLeftPopAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
			.Returns(Task.FromResult((StackExchange.Redis.RedisValue[])null));

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReturn_WhenRedisTimeoutOccurs()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockRedisDatabase
			.Setup(x => x.ListLeftPopAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
			.ThrowsAsync(new RedisTimeoutException("Redis timeout", CommandStatus.Unknown));

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldLogAndCompleteGracefully()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		// Set up for a failed cache operation - returns null
		_fixture.MockRedisDatabase
			.Setup(x => x.ListLeftPopAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
			.Returns(Task.FromResult((StackExchange.Redis.RedisValue[])Array.Empty<StackExchange.Redis.RedisValue>()));

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	#endregion

	#region ProcessForErrorStatusAsync Tests

	[Fact]
	public async Task ProcessForErrorStatusAsync_ShouldReturn_WhenNoErrorBatches()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		_fixture.MockRedisDatabase
			.Setup(x => x.ListLeftPopAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
			.Returns(Task.FromResult((StackExchange.Redis.RedisValue[])null));

		// Act
		Func<Task> act = async () => await service.ProcessForErrorStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessForErrorStatusAsync_ShouldReturn_WhenRedisTimeoutOccurs()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockRedisDatabase
			.Setup(x => x.ListLeftPopAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
			.ThrowsAsync(new RedisTimeoutException("Redis timeout", CommandStatus.Unknown));

		// Act
		Func<Task> act = async () => await service.ProcessForErrorStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ProcessForErrorStatusAsync_ShouldHandleEmptyErrorBatch()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		_fixture.MockRedisDatabase
			.Setup(x => x.ListLeftPopAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
			.Returns(Task.FromResult((StackExchange.Redis.RedisValue[])Array.Empty<StackExchange.Redis.RedisValue>()));

		// Act
		Func<Task> act = async () => await service.ProcessForErrorStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	#endregion
}
