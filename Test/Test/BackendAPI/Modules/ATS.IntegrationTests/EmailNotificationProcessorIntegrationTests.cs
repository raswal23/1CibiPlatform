using System.Text.Json;
using ATS.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class EmailNotificationProcessorIntegrationTests : BaseIntegrationTest
{

	public EmailNotificationProcessorIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	private async Task<List<EmailInvitationRequest>> SeedEmailInvitationRequestsAsync(int count)
	{
		var invitations = new List<EmailInvitationRequest>();

		for (int i = 0; i < count; i++)
		{
			var invitation = new EmailInvitationRequest
			{
				EmailInvitationID = Guid.CreateVersion7(),
				FirstName = $"FirstName{i}",
				LastName = $"LastName{i}",
				MiddleInitial = "M",
				EmailAddress = $"test{i}@example.com",
				MobileNumber = $"09123456{i:D3}",
				HashToken = _hashService.Hash($"token-{i}"),
				HashTokenCreatedAt = DateTime.UtcNow,
				HashTokenExpiration = DateTime.UtcNow.AddHours(24),
				SelectPackage = "Standard",
				RushNormal = "Normal",
				EmailSentStatus = "Pending",
				ApplicationFormStatus = "Pending",
				OrderStatus = "Pending Candidate Info"
			};

			invitations.Add(invitation);
			await _dbContext.EmailInvitationRequests.AddAsync(invitation);
		}

		await _dbContext.SaveChangesAsync();
		return invitations;
	}

	private async Task SeedBatchAsync(IDatabase dbRedis, string batchId, List<EmailInvitationRequest> invitations, long? score = null)
	{
		await dbRedis.StringSetAsync(
			batchId,
			JsonSerializer.Serialize(new List<List<EmailInvitationRequest>> { invitations }),
			TimeSpan.FromDays(2));

		await dbRedis.SortedSetAddAsync(
			"devtest-ats-batches:pending",
			batchId,
			score ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
	}

	#region Positive Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_WithValidBatch_ShouldProcessEmailInvitations()
	{
		// Arrange
		var emailInvitations = await SeedEmailInvitationRequestsAsync(3);
		var batchId = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

		var dbRedis = _redis.GetDatabase();
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:pending");
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:processing");
		await SeedBatchAsync(dbRedis, batchId, emailInvitations);

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		var processedInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => emailInvitations.Select(ei => ei.EmailInvitationID).Contains(e.EmailInvitationID))
			.ToListAsync();

		processedInvitations.Should().NotBeEmpty();
		processedInvitations.Should().AllSatisfy(e =>
		{
			e.EmailSentStatus.Should().NotBe("Pending");
		});

		// Batch is fully cleaned up: no queue entries, no payload
		// (membership checks, not counts — other test classes share these keys in parallel)
		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:pending", batchId)).Should().BeNull();
		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:processing", batchId)).Should().BeNull();
		(await dbRedis.KeyExistsAsync(batchId)).Should().BeFalse();
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_WithNoPendingBatch_ShouldDoNothing()
	{
		// Arrange
		var dbRedis = _redis.GetDatabase();
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:pending");
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:processing");

		var initialCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		var finalCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		finalCount.Should().Be(initialCount);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_WithMultipleBatches_ShouldProcessSequentially()
	{
		// Arrange
		var batch1Invitations = await SeedEmailInvitationRequestsAsync(2);
		var batch2Invitations = await SeedEmailInvitationRequestsAsync(3);

		var batch1Id = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";
		var batch2Id = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow.AddSeconds(1):yyyyMMdd}";

		var dbRedis = _redis.GetDatabase();
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:pending");
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:processing");
		// batch1 gets an explicitly older score so ZPOPMIN deterministically claims it first
		await SeedBatchAsync(dbRedis, batch1Id, batch1Invitations, DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds());
		await SeedBatchAsync(dbRedis, batch2Id, batch2Invitations);

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert — one invocation claims exactly one batch (the oldest); the other stays pending
		var batch1Processed = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => batch1Invitations.Select(ei => ei.EmailInvitationID).Contains(e.EmailInvitationID))
			.ToListAsync();

		batch1Processed.Should().NotBeEmpty();
		batch1Processed.Should().AllSatisfy(e => e.EmailSentStatus.Should().NotBe("Pending"));

		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:pending", batch1Id)).Should().BeNull();
		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:pending", batch2Id)).Should().NotBeNull();
	}

	[Fact]
	public async Task RequeueStaleBatchesAsync_WithStaleBatch_ShouldMoveItBackToPending()
	{
		// Arrange
		var batchId = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

		var dbRedis = _redis.GetDatabase();
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:pending");
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:processing");

		// Simulate a batch claimed 25 hours ago and never finished
		await dbRedis.SortedSetAddAsync(
			"devtest-ats-batches:processing",
			batchId,
			DateTimeOffset.UtcNow.AddHours(-25).ToUnixTimeSeconds());

		// Act
		await _emailNotificationRecoveryService.RequeueStaleBatchesAsync(CancellationToken.None);

		// Assert
		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:pending", batchId)).Should().NotBeNull();
		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:processing", batchId)).Should().BeNull();
	}

	[Fact]
	public async Task RequeueStaleBatchesAsync_WithFreshBatch_ShouldLeaveItInProcessing()
	{
		// Arrange
		var batchId = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

		var dbRedis = _redis.GetDatabase();
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:pending");
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:processing");

		await dbRedis.SortedSetAddAsync(
			"devtest-ats-batches:processing",
			batchId,
			DateTimeOffset.UtcNow.ToUnixTimeSeconds());

		// Act
		await _emailNotificationRecoveryService.RequeueStaleBatchesAsync(CancellationToken.None);

		// Assert
		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:processing", batchId)).Should().NotBeNull();
		(await dbRedis.SortedSetScoreAsync("devtest-ats-batches:pending", batchId)).Should().BeNull();
	}
	#endregion

}
