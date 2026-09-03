using ATS.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class BulkSubmissionProcessorIntegrationTests : BaseIntegrationTest
{

	public BulkSubmissionProcessorIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	private async Task<BulkUploadFileDetails> SeedBulkUploadFileAsync(
		string fileName,
		string packageType,
		string orderType,
		string? csvContent = null)
	{
		csvContent ??= """
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
			PackageId = DefaultPackageId,
			PackageType = packageType,
			OrderType = orderType,
			UploadedByUserId = Guid.CreateVersion7(),
			Status = "Pending",
			DateCreated = DateTime.UtcNow
		};

		await _dbContext.BulkUploadFileDetails.AddAsync(bulkFile);
		await _dbContext.SaveChangesAsync();

		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));
		await _objectStorageService.UploadAsync("test", fileName, stream);

		return bulkFile;
	}

	#region Positive Path
	[Fact]
	public async Task ProcessAsync_WithValidBulkUploadFile_ShouldCreateEmailInvitationRequests()
	{
		// Arrange
		var bulkFile = await SeedBulkUploadFileAsync("test_file.csv", "Standard", "Normal");

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(CancellationToken.None);

		// Assert 
		var emailInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.SelectPackage == "Standard" && e.RushNormal == "Normal")
			.ToListAsync();

		emailInvitations.Should().NotBeEmpty();
		emailInvitations.Should().AllSatisfy(e =>
		{
			e.HashToken.Should().NotBeNullOrEmpty();
			e.ApplicationFormStatus.Should().Be("Pending");
			e.EmailSentStatus.Should().Be("Pending");
		});
	}

	[Fact]
	public async Task ProcessAsync_WithNoPendingFiles_ShouldDoNothing()
	{
		// Arrange
		var initialCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(CancellationToken.None);

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

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(CancellationToken.None);

		// Assert 
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
	#endregion

	[Fact]
	public async Task ProcessAsync_WithExtraColumnsAfterMobileNumber_ShouldProcessAndIgnoreThem()
	{
		// Operators' spreadsheets often carry spare columns after MobileNumber. The
		// import maps by header name, so those columns must be ignored, not fatal.
		var csvContent = """
		LastName,FirstName,MiddleInitial,EmailAddress,MobileNumber,Notes,Extra
		Dela Cruz,Juan,S,juan-extra@example.com,+639171234567,some note,x
		Santos,Maria,A,maria-extra@example.com,+639178765432,,
		""";

		var bulkFile = await SeedBulkUploadFileAsync(
			"extra-columns.csv",
			"Standard",
			"Normal",
			csvContent);

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(CancellationToken.None);

		// Assert - both rows imported, extra columns silently dropped
		var emailInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.BulkFileID == bulkFile.FileID)
			.ToListAsync();

		emailInvitations.Should().HaveCount(2);
		emailInvitations.Select(e => e.LastName).Should().BeEquivalentTo("Dela Cruz", "Santos");
	}

	#region Negative Path
	[Fact]
	public async Task ProcessAsync_WithEmptyCsvHeader_ShouldMarkFileAsPending()
	{
		// Arrange
		var csvContent = string.Empty;

		var bulkFile = await SeedBulkUploadFileAsync(
			"empty-header.csv",
			"Standard",
			"Normal",
			csvContent);

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(CancellationToken.None);

		// Assert - File should remain Pending due to error, retry later
		var fileAfterProcess = await _dbContext.BulkUploadFileDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(f => f.FileID == bulkFile.FileID);

		fileAfterProcess.Should().NotBeNull();
		fileAfterProcess!.Status.Should().Be("Pending");

		// No email invitations should be created
		var emailInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.BulkFileID == bulkFile.FileID)
			.ToListAsync();

		emailInvitations.Should().BeEmpty();
	}

	[Fact]
	public async Task ProcessAsync_WithSwappedColumnOrder_ShouldMarkFileAsPending()
	{
		// All five template columns present, wrong sequence. The template's order is
		// the standard, so this must be rejected, not remapped.
		var csvContent = """
		FirstName,LastName,MiddleInitial,EmailAddress,MobileNumber
		Juan,Dela Cruz,S,juan-swap@example.com,+639171234567
		""";

		var bulkFile = await SeedBulkUploadFileAsync(
			"swapped-columns.csv",
			"Standard",
			"Normal",
			csvContent);

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(CancellationToken.None);

		// Assert - File should remain Pending due to header validation error
		var fileAfterProcess = await _dbContext.BulkUploadFileDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(f => f.FileID == bulkFile.FileID);

		fileAfterProcess.Should().NotBeNull();
		fileAfterProcess!.Status.Should().Be("Pending");

		var emailInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.BulkFileID == bulkFile.FileID)
			.ToListAsync();

		emailInvitations.Should().BeEmpty();
	}

	[Fact]
	public async Task ProcessAsync_WithInvalidColumnHeaders_ShouldMarkFileAsPending()
	{
		// Arrange
		var csvContent = """
		Surname,GivenName,MiddleName,Email,Phone
		Dela Cruz,Juan,S,juan@example.com,+639171234567
		Santos,Maria,A,maria@example.com,+639178765432
		""";

		var bulkFile = await SeedBulkUploadFileAsync(
			"invalid-header.csv",
			"Standard",
			"Normal",
			csvContent);

		// Act
		await _bulkSubmissionProcessorService.ProcessAsync(CancellationToken.None);

		// Assert - File should remain Pending due to header validation error, retry later
		var fileAfterProcess = await _dbContext.BulkUploadFileDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(f => f.FileID == bulkFile.FileID);

		fileAfterProcess.Should().NotBeNull();
		fileAfterProcess!.Status.Should().Be("Pending");

		// No email invitations should be created
		var emailInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => e.BulkFileID == bulkFile.FileID)
			.ToListAsync();

		emailInvitations.Should().BeEmpty();
	}
	#endregion

}
