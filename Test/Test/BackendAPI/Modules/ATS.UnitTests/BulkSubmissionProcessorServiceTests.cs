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

		// The fixture is shared across the class, so clear per-test state. Mocks that
		// are configured in the fixture constructor only have their invocation history
		// cleared, so their setups survive.
		_fixture.MockRepository.Reset();
		_fixture.MockObjectStorage.Reset();
		_fixture.MockSecureToken.Reset();
		_fixture.MockHashService.Reset();
		_fixture.MockHubContext.Invocations.Clear();
		_fixture.MockClients.Invocations.Clear();
		_fixture.MockATSClient.Invocations.Clear();
	}

	#region Positive Path

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

		_fixture.MockRepository.Setup(x => x.UpdateBulkFileDetailsStatusAsync(It.IsAny<List<Guid>>(), It.IsAny<string>()))
			.ReturnsAsync(true);

		// Act
		Func<Task> act = async () => await service.ProcessAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();

		_fixture.MockRepository.Verify(
			x => x.AddBulkEmailInvitationRequestAsync(It.IsAny<List<EmailInvitationRequest>>()),
			Times.Once);

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkFileDetailsStatusAsync(It.IsAny<List<Guid>>(), It.IsAny<string>()),
			Times.AtLeastOnce);

		_fixture.MockHubContext.Verify(
			x => x.Clients,
			Times.Once);
	}
	#endregion

	#region Negative Path
	[Fact]
	public async Task ProcessAsync_ShouldLeaveFilePending_WhenGenerateTokenFails()
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

		// Assert: the failure is contained and the file stays Pending for the next tick.
		await act.Should().NotThrowAsync();

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkFileDetailsStatusAsync(It.IsAny<List<BulkUploadFileDetails>>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessAsync_ShouldLeaveFilePending_WhenHashTokenFails()
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

		// Assert: the failure is contained and the claim is released back to Pending.
		await act.Should().NotThrowAsync();

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkFileDetailsStatusAsync(It.IsAny<List<BulkUploadFileDetails>>()),
			Times.Never);

		_fixture.MockRepository.Verify(
			x => x.ReleaseBulkFileClaimsAsync(
				It.Is<List<BulkUploadFileDetails>>(list => list.Count == 1 && list[0].FileID == fileId)),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldReleaseStaleClaims_BeforeClaimingWork()
	{
		// Arrange
		var service = _fixture.BulkSubmissionProcessorService;

		_fixture.MockRepository
			.Setup(x => x.ReleaseStaleBulkFileClaimsAsync(It.IsAny<TimeSpan>()))
			.ReturnsAsync(2);

		_fixture.MockRepository.Setup(x => x.GetBulkUploadFileDetailsAsync())
			.ReturnsAsync(new List<BulkUploadFileDetails>());

		// Act
		await service.ProcessAsync(CancellationToken.None);

		// Assert: files stranded in Processing by a crashed worker are recovered.
		_fixture.MockRepository.Verify(
			x => x.ReleaseStaleBulkFileClaimsAsync(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldLeaveFilePending_WhenDownloadFails()
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

		// Assert: a transient OSS failure leaves the file Pending so the next tick
		// retries it.
		await act.Should().NotThrowAsync();

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkFileDetailsStatusAsync(It.IsAny<List<BulkUploadFileDetails>>()),
			Times.Never);
	}
	#endregion


}
