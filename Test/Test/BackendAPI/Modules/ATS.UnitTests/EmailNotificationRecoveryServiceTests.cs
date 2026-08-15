using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class EmailNotificationRecoveryServiceTests : IClassFixture<ATSServiceFixture>
{
	private readonly ATSServiceFixture _fixture;

	public EmailNotificationRecoveryServiceTests(ATSServiceFixture fixture)
	{
		_fixture = fixture;
	}

	#region Positive Path
	[Fact]
	public async Task RequeueStaleBatchesAsync_ShouldRequeueEachStaleBatch()
	{
		// Arrange
		var service = _fixture.EmailNotificationRecoveryService;
		_fixture.MockRedisDatabase.Invocations.Clear();

		var staleBatch1 = (RedisValue)$"batch:{Guid.CreateVersion7():N}:20260810";
		var staleBatch2 = (RedisValue)$"batch:{Guid.CreateVersion7():N}:20260811";

		_fixture.MockRedisDatabase
			.Setup(x => x.SortedSetRangeByScoreAsync(
				"ats-batches-processing",
				It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(),
				It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
			.ReturnsAsync([staleBatch1, staleBatch2]);

		// Act
		await service.RequeueStaleBatchesAsync(CancellationToken.None);

		// Assert
		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetAddAsync("ats-batches-pending", staleBatch1, It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()),
			Times.Once);
		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetAddAsync("ats-batches-pending", staleBatch2, It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()),
			Times.Once);
		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetRemoveAsync("ats-batches-processing", staleBatch1, It.IsAny<CommandFlags>()),
			Times.Once);
		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetRemoveAsync("ats-batches-processing", staleBatch2, It.IsAny<CommandFlags>()),
			Times.Once);
	}

	[Fact]
	public async Task RequeueStaleBatchesAsync_ShouldDoNothing_WhenNoStaleBatches()
	{
		// Arrange
		var service = _fixture.EmailNotificationRecoveryService;
		_fixture.MockRedisDatabase.Invocations.Clear();

		_fixture.MockRedisDatabase
			.Setup(x => x.SortedSetRangeByScoreAsync(
				"ats-batches-processing",
				It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(),
				It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
			.ReturnsAsync(Array.Empty<RedisValue>());

		// Act
		await service.RequeueStaleBatchesAsync(CancellationToken.None);

		// Assert
		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()),
			Times.Never);
		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()),
			Times.Never);
	}
	#endregion

	#region Negative Path
	[Fact]
	public async Task RequeueStaleBatchesAsync_ShouldReturn_WhenRedisTimeoutOccurs()
	{
		// Arrange
		var service = _fixture.EmailNotificationRecoveryService;
		_fixture.MockRedisDatabase.Invocations.Clear();

		_fixture.MockRedisDatabase
			.Setup(x => x.SortedSetRangeByScoreAsync(
				"ats-batches-processing",
				It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(),
				It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
			.ThrowsAsync(new RedisTimeoutException("Redis timeout", CommandStatus.Unknown));

		// Act
		Func<Task> act = async () => await service.RequeueStaleBatchesAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		_fixture.MockRedisDatabase.Verify(
			x => x.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()),
			Times.Never);
	}
	#endregion
}
