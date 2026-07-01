using ATS.Data.Entities;
using ATS.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class BulkSubmissionProcessorIntegrationTests : BaseIntegrationTest, IClassFixture<IntegrationTestWebAppFactory>
{
	private readonly IBulkSubmissionProcessorService _bulkSubmissionProcessorService;
	private readonly IConnectionMultiplexer _redis;
	private readonly IntegrationTestWebAppFactory _factory;

	public BulkSubmissionProcessorIntegrationTests(IntegrationTestWebAppFactory factory) 
		: base(factory)
	{
		_factory = factory;
		_bulkSubmissionProcessorService = factory.Services.GetRequiredService<IBulkSubmissionProcessorService>();
		_redis = factory.Services.GetRequiredService<IConnectionMultiplexer>();
	}

	[Fact]
	public async Task ProcessAsync_WithValidBulkUploadFile_ShouldCreateEmailInvitationRequests()
	{
		// Arrange
		var bulkFile = await SeedBulkUploadFileAsync("test_file.csv", "Standard", "Normal");
		var cancellationToken = CancellationToken.None;

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(cancellationToken);

		// Assert - Use AsNoTracking to avoid DbContext conflicts from concurrent operations
		var emailInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.SelectPackage == "Standard" && e.RushNormal == "Normal")
			.ToListAsync();

		emailInvitations.Should().NotBeEmpty();
		emailInvitations.Should().AllSatisfy(e =>
		{
			e.HashToken.Should().NotBeNullOrEmpty();
			e.HashTokenCreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
			e.IsFormCompleted.Should().BeFalse();
			e.EmailSentStatus.Should().Be("Pending");
		});
	}

	[Fact]
	public async Task ProcessAsync_WithNoPendingFiles_ShouldDoNothing()
	{
		// Arrange
		var cancellationToken = CancellationToken.None;
		var initialCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(cancellationToken);

		// Assert
		var finalCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();
		finalCount.Should().Be(initialCount);
	}

	[Fact]
	public async Task ProcessAsync_WithMultipleBulkFiles_ShouldProcessAllConcurrently()
	{
		// Arrange
		await SeedBulkUploadFileAsync("file1.csv", "Premium", "Rush");
		await SeedBulkUploadFileAsync("file2.csv", "Standard", "Normal");
		var cancellationToken = CancellationToken.None;

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(cancellationToken);

		// Assert - Use AsNoTracking to prevent tracking conflicts
		var premiumInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.SelectPackage == "Premium")
			.ToListAsync();

		var standardInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.SelectPackage == "Standard")
			.ToListAsync();

		premiumInvitations.Should().NotBeEmpty();
		standardInvitations.Should().NotBeEmpty();
	}

	private async Task<BulkUploadFileDetails> SeedBulkUploadFileAsync(string fileName, string packageType, string orderType)
	{
		var csvContent = """
			LastName,FirstName,MiddleInitial,EmailAddress,MobileNumber
			Dela Cruz,Juan,S,juan@example.com,+639171234567
			Santos,Maria,A,maria@example.com,+639178765432
			Reyes,Carlos,R,carlos@example.com,+639179876543
			""";

		var bulkFile = new BulkUploadFileDetails
		{
			FileID = Guid.CreateVersion7(),
			FileName = fileName,
			FileKey = $"test/{fileName}",
			PackageType = packageType,
			OrderType = orderType,
			UploadedByUserId = Guid.CreateVersion7(),
			Status = "Pending",
			DateCreated = DateTime.UtcNow
		};

		await _dbContext.BulkUploadFileDetails.AddAsync(bulkFile);
		await _dbContext.SaveChangesAsync();

		// Upload CSV content to object storage
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));
		await _objectStorageService.UploadAsync("test", fileName, stream);

		return bulkFile;
	}
}
