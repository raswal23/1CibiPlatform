using System.IO.Compression;
using System.Text;
using ATS.Data.DTO;
using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.Data.UnitOfWork;
using ATS.DTO;
using ATS.Services.OrderHistory;
using ATS.Constants;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ATS.Services.Report;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class ReportServiceTests
{
	private const string ReportFolder = "ats-reports";
	private const string UploadedFileKey = "ats-reports/report.pdf";

	private readonly Mock<ILogger<ReportService>> _logger = new();
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IObjectStorageService> _objectStorage = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();
	private readonly Mock<IUserClientRepository> _userClientRepository = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly Mock<IUnitOfWork> _unitOfWork = new();
	private readonly ReportService _service;

	public ReportServiceTests()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ATS:ATSReportFileFolderName"] = ReportFolder
			})
			.Build();

		_service = new ReportService(
			_logger.Object,
			_repository.Object,
			configuration,
			_objectStorage.Object,
			_orderHistoryService.Object,
			_userClientRepository.Object,
			_currentUser.Object,
			_unitOfWork.Object);
	}

	#region Happy Path

	[Theory]
	[InlineData("Initial Report", "In Progress", false)]
	[InlineData("Complete Final Report", "Completed", true)]
	public async Task UploadReportAsync_ShouldAddNewReportAndSetExpectedOrderStatus(
		string reportStatus,
		string expectedOrderStatus,
		bool shouldCompleteOrder)
	{
		// Arrange
		var request = CreateUploadRequest(reportStatus);
		var invitation = CreateInvitation(request.EmailInvitationRequestId);
		ReportDetails? addedReport = null;
		DateTime? orderCompletedAt = null;

		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				request.EmailInvitationRequestId,
				CancellationToken.None))
			.ReturnsAsync(invitation);
		_objectStorage
			.Setup(storage => storage.UploadAsync(
				ReportFolder,
				"report.pdf",
				It.IsAny<Stream>(),
				CancellationToken.None))
			.ReturnsAsync(UploadedFileKey);
		_repository
			.Setup(repository => repository.GetReportDetailsByStatusAsync(
				request.EmailInvitationRequestId,
				reportStatus,
				CancellationToken.None))
			.ReturnsAsync((ReportDetails?)null);
		_repository
			.Setup(repository => repository.UpdateOrderStatusAsync(
				request.EmailInvitationRequestId,
				expectedOrderStatus,
				It.IsAny<DateTime?>(),
				CancellationToken.None))
			.Callback<Guid, string, DateTime?, CancellationToken>((_, _, completedAt, _) =>
				orderCompletedAt = completedAt)
			.ReturnsAsync(true);
		_repository
			.Setup(repository => repository.AddReportDetailsAsync(
				It.IsAny<ReportDetails>(),
				CancellationToken.None))
			.Callback<ReportDetails, CancellationToken>((report, _) => addedReport = report)
			.ReturnsAsync(true);

		// Act
		var result = await _service.UploadReportAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		addedReport.Should().NotBeNull();
		addedReport!.ReportFileId.Should().NotBe(Guid.Empty);
		addedReport.EmailInvitationRequestId.Should().Be(request.EmailInvitationRequestId);
		addedReport.HitStatus.Should().Be("Clear");
		addedReport.ReportStatus.Should().Be(reportStatus);
		addedReport.ReportFileName.Should().Be("report.pdf");
		addedReport.ReportFileKey.Should().Be(UploadedFileKey);
		addedReport.ReportUploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

		if (shouldCompleteOrder)
			orderCompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
		else
			orderCompletedAt.Should().BeNull();

		_repository.Verify(repository => repository.UpdateReportDetailsAsync(
			It.IsAny<ReportDetails>(),
			It.IsAny<CancellationToken>()), Times.Never);
		_repository.Verify(repository => repository.AddArchiveReportAsync(
			It.IsAny<ArchiveReport>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task UploadReportAsync_ShouldArchiveAndUpdateExistingReport()
	{
		// Arrange
		var request = CreateUploadRequest("Supplementary Report", "replacement.pdf", "Not Clear");
		var invitation = CreateInvitation(request.EmailInvitationRequestId);
		var originalUploadedAt = new DateTime(2026, 7, 1, 8, 30, 0, DateTimeKind.Utc);
		var existingReport = new ReportDetails
		{
			ReportFileId = Guid.CreateVersion7(),
			EmailInvitationRequestId = request.EmailInvitationRequestId,
			HitStatus = "Clear",
			ReportStatus = "Supplementary Report",
			ReportFileName = "original.pdf",
			ReportFileKey = "ats-reports/original.pdf",
			ReportUploadedAt = originalUploadedAt
		};
		ArchiveReport? archivedReport = null;
		ReportDetails? updatedReport = null;

		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				request.EmailInvitationRequestId,
				CancellationToken.None))
			.ReturnsAsync(invitation);
		_objectStorage
			.Setup(storage => storage.UploadAsync(
				ReportFolder,
				"replacement.pdf",
				It.IsAny<Stream>(),
				CancellationToken.None))
			.ReturnsAsync("ats-reports/replacement.pdf");
		_repository
			.Setup(repository => repository.GetReportDetailsByStatusAsync(
				request.EmailInvitationRequestId,
				"Supplementary Report",
				CancellationToken.None))
			.ReturnsAsync(existingReport);
		_repository
			.Setup(repository => repository.UpdateOrderStatusAsync(
				request.EmailInvitationRequestId,
				"Completed",
				It.IsAny<DateTime?>(),
				CancellationToken.None))
			.ReturnsAsync(true);
		_repository
			.Setup(repository => repository.AddArchiveReportAsync(
				It.IsAny<ArchiveReport>(),
				CancellationToken.None))
			.Callback<ArchiveReport, CancellationToken>((archive, _) => archivedReport = archive)
			.ReturnsAsync(true);
		_repository
			.Setup(repository => repository.UpdateReportDetailsAsync(
				It.IsAny<ReportDetails>(),
				CancellationToken.None))
			.Callback<ReportDetails, CancellationToken>((report, _) => updatedReport = report)
			.ReturnsAsync(true);

		// Act
		var result = await _service.UploadReportAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		archivedReport.Should().NotBeNull();
		archivedReport!.ArchiveReportId.Should().NotBe(Guid.Empty);
		archivedReport.EmailInvitationRequestId.Should().Be(request.EmailInvitationRequestId);
		archivedReport.ReportStatus.Should().Be("Supplementary Report");
		archivedReport.ReportFileKey.Should().Be("ats-reports/original.pdf");
		archivedReport.ReportUploadedAt.Should().Be(originalUploadedAt);

		updatedReport.Should().BeSameAs(existingReport);
		updatedReport!.HitStatus.Should().Be("Not Clear");
		updatedReport.ReportFileName.Should().Be("replacement.pdf");
		updatedReport.ReportFileKey.Should().Be("ats-reports/replacement.pdf");
		updatedReport.ReportUploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
		_repository.Verify(repository => repository.AddReportDetailsAsync(
			It.IsAny<ReportDetails>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetReportsAsync_ShouldUseUnfilteredQuery_WhenNoSearchOrDateFilterIsProvided()
	{
		// Arrange
		var userId = SetAuthenticatedUser(AtsRoleIds.User, clientId: 7);
		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 10);
		var rows = CreateReportRows();
		_repository
			.Setup(repository => repository.GetReportsPageAsync(
				null,
				null,
				null,
				11,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
				userId,
				CancellationToken.None))
			.ReturnsAsync(rows.ToList());
		_repository
			.Setup(repository => repository.CountReportsAsync(
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 7 })),
				userId,
				CancellationToken.None))
			.ReturnsAsync(1);

		// Act
		var result = await _service.GetReportsAsync(request, CancellationToken.None);

		// Assert
		result.Items.Should().ContainSingle().Which.SubjectName.Should().Be("Ada Lovelace");
		result.TotalCount.Should().Be(1);
		_repository.Verify(repository => repository.SearchReportsPageAsync(
			It.IsAny<int?>(),
			It.IsAny<DateTime?>(),
			It.IsAny<Guid?>(),
			It.IsAny<int>(),
			It.IsAny<string?>(),
			It.IsAny<DateTime?>(),
			It.IsAny<DateTime?>(),
			It.IsAny<IReadOnlyCollection<int>>(),
			It.IsAny<Guid?>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetReportsAsync_ShouldUseSearchQuery_WhenAnyFilterIsProvided()
	{
		// Arrange
		var userId = SetAuthenticatedUser(AtsRoleIds.Admin, clientId: 99);
		_userClientRepository.Setup(repository => repository.GetUserClientAssignmentsAsync(
			It.Is<IReadOnlyCollection<Guid>>(userIds => userIds.SequenceEqual(new[] { userId })),
			CancellationToken.None)).ReturnsAsync(
			[
				new UserClientDetailsDTO { UserId = userId, ClientId = 1 },
				new UserClientDetailsDTO { UserId = userId, ClientId = 3 }
			]);
		var request = new KeysetPaginationRequest(
			Cursor: null,
			PageSize: 10,
			SearchTerm: "ada",
			StartDate: new DateTime(2026, 8, 1),
			EndDate: new DateTime(2026, 8, 31));
		var rows = CreateReportRows();
		_repository
			.Setup(repository => repository.SearchReportsPageAsync(
				null,
				null,
				null,
				11,
				"ada",
				request.StartDate,
				request.EndDate,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 1, 3 })),
				null,
				CancellationToken.None))
			.ReturnsAsync(rows.ToList());
		_repository
			.Setup(repository => repository.CountSearchReportsAsync(
				"ada",
				request.StartDate,
				request.EndDate,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.SequenceEqual(new[] { 1, 3 })),
				null,
				CancellationToken.None))
			.ReturnsAsync(1);

		// Act
		var result = await _service.GetReportsAsync(request, CancellationToken.None);

		// Assert
		result.Items.Should().ContainSingle().Which.SubjectName.Should().Be("Ada Lovelace");
		_repository.Verify(repository => repository.GetReportsPageAsync(
			It.IsAny<int?>(),
			It.IsAny<DateTime?>(),
			It.IsAny<Guid?>(),
			It.IsAny<int>(),
			It.IsAny<IReadOnlyCollection<int>>(),
			It.IsAny<Guid?>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetReportsAsync_ShouldBypassAllDataFilters_ForPlatformSuperAdmin()
	{
		var request = new KeysetPaginationRequest(Cursor: null, PageSize: 10);
		var rows = CreateReportRows();
		SetAuthenticatedUser(AtsRoleIds.User, clientId: 7);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns((int?)null);
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(true);
		_repository.Setup(repository => repository.GetReportsPageAsync(
			null,
			null,
			null,
			11,
			null,
			null,
			CancellationToken.None)).ReturnsAsync(rows.ToList());
		_repository.Setup(repository => repository.CountReportsAsync(
			null,
			null,
			CancellationToken.None)).ReturnsAsync(1);

		var result = await _service.GetReportsAsync(request, CancellationToken.None);

		result.Items.Should().ContainSingle();
		_userClientRepository.Verify(repository => repository.GetUserClientAssignmentsAsync(
			It.IsAny<IReadOnlyCollection<Guid>>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetReportResultByEmailInvitationRequestIdAsync_ShouldNormalizeMissingValues()
	{
		// Arrange
		var invitationId = Guid.CreateVersion7();
		var expected = new ReportResultDTO
		{
			SubjectName = "Ada Lovelace",
			HitStatus = " ",
			DiplomaFileKey = "documents/diploma.pdf",
			BiometricPhotoFileKey = "documents/photo.jpg",
			FilledFormAt = "August 01, 2026"
		};
		_repository
			.Setup(repository => repository.GetReportResultByEmailInvitationRequestIdAsync(
				invitationId,
				CancellationToken.None))
			.ReturnsAsync(expected);

		// Act
		var result = await _service.GetReportResultByEmailInvitationRequestIdAsync(
			invitationId,
			CancellationToken.None);

		// Assert
		result.Should().BeSameAs(expected);
		result.HitStatus.Should().Be("-");
		result.UploadDiplomaAt.Should().Be(expected.FilledFormAt);
		result.UploadBiometricPhotoAt.Should().Be(expected.FilledFormAt);
	}

	[Fact]
	public async Task DownloadIndividualReportAsync_ShouldReturnZipContainingEveryRequestedFile()
	{
		// Arrange
		var request = new DownloadIndividualDocumentsRequestDTO
		{
			SubjectName = "Ada Lovelace",
			FileDocuments =
			[
				new DownloadIndividualDocuments
				{
					FileKey = "documents/resume.pdf",
					FileName = "resume.pdf"
				},
				new DownloadIndividualDocuments
				{
					FileKey = "documents/id.pdf",
					FileName = "id.pdf"
				}
			]
		};
		_objectStorage
			.Setup(storage => storage.DownloadAsync("documents/resume.pdf", CancellationToken.None))
			.ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("resume-content")));
		_objectStorage
			.Setup(storage => storage.DownloadAsync("documents/id.pdf", CancellationToken.None))
			.ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("id-content")));

		// Act
		await using var result = await _service.DownloadIndividualReportAsync(
			request,
			CancellationToken.None);

		// Assert
		using var archive = new ZipArchive(result, ZipArchiveMode.Read);
		archive.Entries.Select(entry => entry.FullName).Should().Equal("resume.pdf", "id.pdf");
		(await ReadEntryAsync(archive.GetEntry("resume.pdf")!)).Should().Be("resume-content");
		(await ReadEntryAsync(archive.GetEntry("id.pdf")!)).Should().Be("id-content");
	}

	[Fact]
	public async Task DownloadMultipleOrderRecordsAsync_ShouldMergeApplicantDocumentsIntoOnePdfPerApplicant()
	{
		// Arrange
		var invitationId = Guid.CreateVersion7();
		var request = new DownloadMultipleOrderRecordsRequestDTO
		{
			EmailInvitaionRequestList = [invitationId]
		};
		var documents = new List<DownloadDocumentDTO>
		{
			new()
			{
				EmailInvitationRequestId = invitationId,
				SubjectName = "Ada Lovelace",
				FileName = "resume.pdf",
				FileKey = "documents/resume.pdf"
			},
			new()
			{
				EmailInvitationRequestId = invitationId,
				SubjectName = "Ada Lovelace",
				FileName = "id.pdf",
				FileKey = "documents/id.pdf"
			}
		};
		var onePagePdf = CreatePdfBytes();

		_repository
			.Setup(repository => repository.GetDownloadDocumentsAsync(
				request.EmailInvitaionRequestList,
				CancellationToken.None))
			.ReturnsAsync(documents);
		_objectStorage
			.Setup(storage => storage.DownloadAsync(
				It.IsAny<string>(),
				CancellationToken.None))
			.Returns(() => Task.FromResult<Stream>(new MemoryStream(onePagePdf)));

		// Act
		await using var result = await _service.DownloadMultipleOrderRecordsAsync(
			request,
			CancellationToken.None);

		// Assert
		using var archive = new ZipArchive(result, ZipArchiveMode.Read);
		archive.Entries.Should().ContainSingle();
		archive.Entries[0].FullName.Should().Be("Ada_Lovelace.pdf");

		using var mergedPdfStream = new MemoryStream();
		await using (var entryStream = archive.Entries[0].Open())
		{
			await entryStream.CopyToAsync(mergedPdfStream);
		}

		mergedPdfStream.Position = 0;
		using var mergedDocument = PdfReader.Open(mergedPdfStream, PdfDocumentOpenMode.Import);
		mergedDocument.PageCount.Should().Be(2);
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task UploadReportAsync_ShouldThrowBadRequestException_WhenFileIsMissing()
	{
		// Arrange
		var request = new ReportDetailsDTO
		{
			EmailInvitationRequestId = Guid.CreateVersion7(),
			HitStatus = "Clear",
			ReportStatus = "Initial Report",
			ReportFile = null
		};

		// Act
		Func<Task> act = () => _service.UploadReportAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("Report file is required.");
		_repository.Verify(repository => repository.GetEmailInvitationRequestByIdAsync(
			It.IsAny<Guid>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task UploadReportAsync_ShouldThrowNotFoundException_WhenInvitationDoesNotExist()
	{
		// Arrange
		var request = CreateUploadRequest("Initial Report");
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				request.EmailInvitationRequestId,
				CancellationToken.None))
			.ReturnsAsync(new EmailInvitationRequest());

		// Act
		Func<Task> act = () => _service.UploadReportAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"Email invitation with ID {request.EmailInvitationRequestId} not found.");
		_objectStorage.Verify(storage => storage.UploadAsync(
			It.IsAny<string>(),
			It.IsAny<string>(),
			It.IsAny<Stream>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task UploadReportAsync_ShouldDeleteUploadedFileAndWrapFailure_WhenPersistenceFails()
	{
		// Arrange
		var request = CreateUploadRequest("Initial Report");
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				request.EmailInvitationRequestId,
				CancellationToken.None))
			.ReturnsAsync(CreateInvitation(request.EmailInvitationRequestId));
		_objectStorage
			.Setup(storage => storage.UploadAsync(
				ReportFolder,
				"report.pdf",
				It.IsAny<Stream>(),
				CancellationToken.None))
			.ReturnsAsync(UploadedFileKey);
		_repository
			.Setup(repository => repository.GetReportDetailsByStatusAsync(
				request.EmailInvitationRequestId,
				"Initial Report",
				CancellationToken.None))
			.ThrowsAsync(new InvalidOperationException("Database unavailable."));
		_objectStorage
			.Setup(storage => storage.DeleteAsync(UploadedFileKey, CancellationToken.None))
			.Returns(Task.CompletedTask);

		// Act
		Func<Task> act = () => _service.UploadReportAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to upload report. Database unavailable.");
		_objectStorage.Verify(
			storage => storage.DeleteAsync(UploadedFileKey, CancellationToken.None),
			Times.Once);
	}

	[Fact]
	public async Task DownloadIndividualReportAsync_ShouldWrapStorageFailure()
	{
		// Arrange
		var request = new DownloadIndividualDocumentsRequestDTO
		{
			FileDocuments =
			[
				new DownloadIndividualDocuments
				{
					FileKey = "documents/missing.pdf",
					FileName = "missing.pdf"
				}
			]
		};
		_objectStorage
			.Setup(storage => storage.DownloadAsync(
				"documents/missing.pdf",
				CancellationToken.None))
			.ThrowsAsync(new InvalidOperationException("Storage unavailable."));

		// Act
		Func<Task> act = () => _service.DownloadIndividualReportAsync(
			request,
			CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("*Storage unavailable.*");
	}

	[Fact]
	public async Task DownloadMultipleOrderRecordsAsync_ShouldWrapRepositoryFailure()
	{
		// Arrange
		var request = new DownloadMultipleOrderRecordsRequestDTO
		{
			EmailInvitaionRequestList = [Guid.CreateVersion7()]
		};
		_repository
			.Setup(repository => repository.GetDownloadDocumentsAsync(
				request.EmailInvitaionRequestList,
				CancellationToken.None))
			.ThrowsAsync(new InvalidOperationException("Database unavailable."));

		// Act
		Func<Task> act = () => _service.DownloadMultipleOrderRecordsAsync(
			request,
			CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("*Database unavailable.*");
	}

	#endregion

	private Guid SetAuthenticatedUser(int roleId, int clientId)
	{
		var userId = Guid.CreateVersion7();
		_currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		_currentUser.SetupGet(user => user.AtsClientId).Returns(clientId);
		return userId;
	}

	private static ReportDetailsDTO CreateUploadRequest(
		string reportStatus,
		string fileName = "report.pdf",
		string hitStatus = "Clear") => new()
	{
		EmailInvitationRequestId = Guid.CreateVersion7(),
		HitStatus = hitStatus,
		ReportStatus = reportStatus,
		ReportFile = CreateFormFile(fileName)
	};

	private static IFormFile CreateFormFile(string fileName)
	{
		var content = Encoding.UTF8.GetBytes("integration report content");
		return new FormFile(
			new MemoryStream(content),
			0,
			content.Length,
			"ReportFile",
			fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = "application/pdf"
		};
	}

	private static EmailInvitationRequest CreateInvitation(Guid invitationId) => new()
	{
		EmailInvitationID = invitationId,
		FirstName = "Ada",
		LastName = "Lovelace",
		EmailAddress = "ada@example.com",
		MobileNumber = "+639171234567",
		SelectPackage = "Basic Screening",
		RushNormal = "Normal",
		HashToken = $"hash-{invitationId}",
		HashTokenCreatedAt = DateTime.UtcNow,
		HashTokenExpiration = DateTime.UtcNow.AddDays(1),
		EmailSentStatus = "Done",
		ApplicationFormStatus = "Done",
		OrderStatus = "In Progress"
	};

	private static List<ReportRowDTO> CreateReportRows() =>
	[
		new ReportRowDTO
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Ada",
			LastName = "Lovelace",
			OrderStatus = "Completed",
			HitStatus = "Clear"
		}
	];

	private static async Task<string> ReadEntryAsync(ZipArchiveEntry entry)
	{
		await using var stream = entry.Open();
		using var reader = new StreamReader(stream, Encoding.UTF8);
		return await reader.ReadToEndAsync();
	}

	private static byte[] CreatePdfBytes()
	{
		using var document = new PdfDocument();
		document.AddPage();
		using var stream = new MemoryStream();
		document.Save(stream, closeStream: false);
		return stream.ToArray();
	}
}
