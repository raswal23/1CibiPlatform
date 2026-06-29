using ATS.Data.Entities;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class BulkSubmissionProcessorServiceTests : IClassFixture<ATSServiceFixture>
{
	private readonly ATSServiceFixture _fixture;
	public BulkSubmissionProcessorServiceTests(ATSServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ProcessAsync_ShouldReturn_WhenNoPendingFiles()
	{
		// Arrange
		var service = _fixture.BulkSubmissionProcessorService;
		_fixture.MockRepository.Setup(x => x.GetBulkUploadFileDetailsAsync())
			.ReturnsAsync(new List<BulkUploadFileDetails>());

		// Act
		Func<Task> act = async () => await service.ProcessAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ProcessAsync_ShouldThrow_WhenGenerateTokenFails()
	{
		// Arrange
		var service = _fixture.BulkSubmissionProcessorService;
		var fileId = Guid.CreateVersion7();
		var uploadedByUserId = Guid.CreateVersion7();

		var bulkUploadFile = new BulkUploadFileDetails
		{
			FileID = fileId,
			FileKey = "test-file-key",
			FileName = "test-file.csv",
			UploadedByUserId = uploadedByUserId,
			PackageType = "Standard",
			OrderType = "Normal",
			Status = "Pending",
			DateCreated = DateTime.UtcNow
		};

		var csvContent = "FirstName,LastName,MiddleInitial,EmailAddress,MobileNumber\nJuan,Dela Cruz,B,juan@example.com,09123456789";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));

		_fixture.MockRepository.Setup(x => x.GetBulkUploadFileDetailsAsync())
			.ReturnsAsync(new List<BulkUploadFileDetails> { bulkUploadFile });

		_fixture.MockObjectStorage.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(stream);

		_fixture.MockSecureToken.Setup(x => x.GenerateSecureToken())
			.Returns(string.Empty);

		// Act
		Func<Task> act = async () => await service.ProcessAsync(CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<InternalServerException>()
			.WithMessage("Failed to generate Token.");
	}

	[Fact]
	public async Task ProcessAsync_ShouldThrow_WhenHashTokenFails()
	{
		// Arrange
		var service = _fixture.BulkSubmissionProcessorService;
		var fileId = Guid.CreateVersion7();
		var uploadedByUserId = Guid.CreateVersion7();

		var bulkUploadFile = new BulkUploadFileDetails
		{
			FileID = fileId,
			FileKey = "test-file-key",
			FileName = "test-file.csv",
			UploadedByUserId = uploadedByUserId,
			PackageType = "Standard",
			OrderType = "Normal",
			Status = "Pending",
			DateCreated = DateTime.UtcNow
		};

		var csvContent = "FirstName,LastName,MiddleInitial,EmailAddress,MobileNumber\nJuan,Dela Cruz,B,juan@example.com,09123456789";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));

		_fixture.MockRepository.Setup(x => x.GetBulkUploadFileDetailsAsync())
			.ReturnsAsync(new List<BulkUploadFileDetails> { bulkUploadFile });

		_fixture.MockObjectStorage.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(stream);

		_fixture.MockSecureToken.Setup(x => x.GenerateSecureToken())
			.Returns("valid-token");

		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>()))
			.Returns(string.Empty);

		// Act
		Func<Task> act = async () => await service.ProcessAsync(CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<InternalServerException>()
			.WithMessage("Failed to hash Token.");
	}

	[Fact]
	public async Task ProcessAsync_ShouldThrow_WhenDownloadFails()
	{
		// Arrange
		var service = _fixture.BulkSubmissionProcessorService;
		var fileId = Guid.CreateVersion7();
		var uploadedByUserId = Guid.CreateVersion7();

		var bulkUploadFile = new BulkUploadFileDetails
		{
			FileID = fileId,
			FileKey = "test-file-key",
			FileName = "test-file.csv",
			UploadedByUserId = uploadedByUserId,
			PackageType = "Standard",
			OrderType = "Normal",
			Status = "Pending",
			DateCreated = DateTime.UtcNow
		};

		_fixture.MockRepository.Setup(x => x.GetBulkUploadFileDetailsAsync())
			.ReturnsAsync(new List<BulkUploadFileDetails> { bulkUploadFile });

		_fixture.MockObjectStorage
			.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("Download failed"));

		// Act
		Func<Task> act = async () => await service.ProcessAsync(CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("Download failed");
	}

	[Fact]
	public async Task ProcessAsync_ShouldProcessFile_WhenSuccessful()
	{
		// Arrange
		var service = _fixture.BulkSubmissionProcessorService;
		var fileId = Guid.CreateVersion7();
		var uploadedByUserId = Guid.CreateVersion7();

		var bulkUploadFile = new BulkUploadFileDetails
		{
			FileID = fileId,
			FileKey = "test-file-key",
			FileName = "test-file.csv",
			UploadedByUserId = uploadedByUserId,
			PackageType = "Standard",
			OrderType = "Normal",
			Status = "Pending",
			DateCreated = DateTime.UtcNow
		};

		var csvContent = "FirstName,LastName,MiddleInitial,EmailAddress,MobileNumber\nJuan,Dela Cruz,B,juan@example.com,09123456789\nMaria,Santos,G,maria@example.com,09987654321";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));
		stream.Position = 0;

		_fixture.MockRepository.Setup(x => x.GetBulkUploadFileDetailsAsync())
			.ReturnsAsync(new List<BulkUploadFileDetails> { bulkUploadFile });

		_fixture.MockObjectStorage.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(stream);

		_fixture.MockSecureToken.Setup(x => x.GenerateSecureToken())
			.Returns("valid-token-123");

		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>()))
			.Returns((string token) => $"hashed-{token}");

		_fixture.MockRepository.Setup(x => x.AddBulkEmailInvitationRequestAsync(It.IsAny<List<EmailInvitationRequest>>()))
			.ReturnsAsync(true);

		_fixture.MockRepository.Setup(x => x.UpdateBulkFileDetailsStatusAsync(It.IsAny<List<BulkUploadFileDetails>>()))
			.ReturnsAsync(true);

		_fixture.MockRedisDatabase.Setup(x => x.ListRightPushAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<StackExchange.Redis.RedisValue>()))
			.ReturnsAsync(1L);

		// Act
		Func<Task> act = async () => await service.ProcessAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();

		_fixture.MockRepository.Verify(
			x => x.AddBulkEmailInvitationRequestAsync(It.IsAny<List<EmailInvitationRequest>>()),
			Times.Once);

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkFileDetailsStatusAsync(It.IsAny<List<BulkUploadFileDetails>>()),
			Times.Once);

		_fixture.MockHubContext.Verify(
			x => x.Clients,
			Times.Once);
	}

}
