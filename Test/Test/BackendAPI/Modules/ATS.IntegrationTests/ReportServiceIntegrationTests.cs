using System.IO.Compression;
using System.Text;
using ATS.Data.DTO;
using ATS.Data.Entities;
using ATS.DTO;
using ATS.Constants;
using Auth.Constants;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Security.Claims;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class ReportServiceIntegrationTests : BaseIntegrationTest
{
	public ReportServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task UploadReportAsync_ShouldPersistInitialReportUploadFileAndSetOrderInProgress()
	{
		// Arrange
		var invitation = CreateInvitation("Initial", orderStatus: "Pending Candidate Info");
		await AddInvitationsAsync(invitation);

		const string fileName = "initial-report.pdf";
		const string fileContent = "initial report integration content";
		var request = CreateUploadRequest(
			invitation.EmailInvitationID,
			"Initial Report",
			"Clear",
			fileName,
			fileContent);
		var expectedFileKey = BuildReportFileKey(fileName);

		// Act
		var result = await _reportService.UploadReportAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		_dbContext.ChangeTracker.Clear();

		var report = await _dbContext.ReportDetails
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationRequestId == invitation.EmailInvitationID);
		report.ReportFileId.Should().NotBe(Guid.Empty);
		report.HitStatus.Should().Be("Clear");
		report.ReportStatus.Should().Be("Initial Report");
		report.ReportFileName.Should().Be(fileName);
		report.ReportFileKey.Should().Be(expectedFileKey);
		report.ReportUploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

		var persistedInvitation = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == invitation.EmailInvitationID);
		persistedInvitation.OrderStatus.Should().Be("In Progress");
		persistedInvitation.OrderCompletedAt.Should().BeNull();

		await using var storedFile = await _objectStorageService.DownloadAsync(
			expectedFileKey,
			CancellationToken.None);
		(await ReadTextAsync(storedFile)).Should().Be(fileContent);
	}

	[Fact]
	public async Task UploadReportAsync_ShouldArchiveExistingReportAndCompleteOrder()
	{
		// Arrange
		var originalUploadedAt = new DateTime(2026, 7, 1, 8, 30, 0, DateTimeKind.Utc);
		var invitation = CreateInvitation("Replacement", orderStatus: "In Progress");
		invitation.ReportDetails =
		[
			new ReportDetails
			{
				ReportFileId = Guid.CreateVersion7(),
				EmailInvitationRequestId = invitation.EmailInvitationID,
				HitStatus = "Clear",
				ReportStatus = "Complete Final Report",
				ReportFileName = "original.pdf",
				ReportFileKey = "reports/original.pdf",
				ReportUploadedAt = originalUploadedAt
			}
		];
		await AddInvitationsAsync(invitation);

		const string replacementName = "replacement.pdf";
		var request = CreateUploadRequest(
			invitation.EmailInvitationID,
			"Complete Final Report",
			"Not Clear",
			replacementName,
			"replacement report content");
		var expectedFileKey = BuildReportFileKey(replacementName);

		// Act
		var result = await _reportService.UploadReportAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		_dbContext.ChangeTracker.Clear();

		var report = await _dbContext.ReportDetails
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationRequestId == invitation.EmailInvitationID);
		report.ReportFileId.Should().Be(invitation.ReportDetails.Single().ReportFileId);
		report.HitStatus.Should().Be("Not Clear");
		report.ReportFileName.Should().Be(replacementName);
		report.ReportFileKey.Should().Be(expectedFileKey);
		report.ReportUploadedAt.Should().BeAfter(originalUploadedAt);

		var archive = await _dbContext.ArchiveReports
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationRequestId == invitation.EmailInvitationID);
		archive.ArchiveReportId.Should().NotBe(Guid.Empty);
		archive.ReportStatus.Should().Be("Complete Final Report");
		archive.ReportFileKey.Should().Be("reports/original.pdf");
		archive.ReportUploadedAt.Should().BeCloseTo(originalUploadedAt, TimeSpan.FromMilliseconds(1));

		var persistedInvitation = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == invitation.EmailInvitationID);
		persistedInvitation.OrderStatus.Should().Be("Completed");
		persistedInvitation.OrderCompletedAt.Should().BeCloseTo(
			DateTime.UtcNow,
			TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task GetReportsAsync_ShouldReturnLatestHitStatusAndApplySearchAndDateFilters()
	{
		// Arrange
		var ada = CreateInvitation(
			"Ada",
			orderStatus: "Completed",
			orderCompletedAt: new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc));
		ada.FirstName = "Ada";
		ada.LastName = "Lovelace";
		ada.ReportDetails =
		[
			CreateReport(ada.EmailInvitationID, "Initial Report", "Clear", "ada-initial.pdf", new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc)),
			CreateReport(ada.EmailInvitationID, "Supplementary Report", "Not Clear", "ada-supplementary.pdf", new DateTime(2026, 8, 14, 8, 0, 0, DateTimeKind.Utc))
		];

		var grace = CreateInvitation(
			"Grace",
			orderStatus: "Completed",
			orderCompletedAt: new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));
		grace.FirstName = "Grace";
		grace.LastName = "Hopper";

		var pending = CreateInvitation("Pending", orderStatus: "In Progress");
		var requestorId = Guid.CreateVersion7();
		ada.RequestorId = requestorId;
		grace.RequestorId = requestorId;
		pending.RequestorId = requestorId;
		await AddInvitationsAsync(ada, grace, pending);
		SetReportScope(AtsRoleIds.User, null, requestorId);

		// Act
		var unfiltered = await _reportService.GetReportsAsync(
			new PaginationRequest(PageIndex: 1, PageSize: 10),
			sortColumn: null,
			sortDescending: false,
			CancellationToken.None);
		var filtered = await _reportService.GetReportsAsync(
			new PaginationRequest(
				PageIndex: 1,
				PageSize: 10,
				SearchTerm: "ada lovelace",
				StartDate: new DateTime(2026, 8, 1),
				EndDate: new DateTime(2026, 8, 31)),
			sortColumn: "SubjectName",
			sortDescending: false,
			CancellationToken.None);

		// Assert
		unfiltered.Count.Should().Be(3);
		unfiltered.Data.Should().HaveCount(3);
		unfiltered.Data.First().EmailInvitationRequestId.Should().Be(grace.EmailInvitationID);
		unfiltered.Data.Single(item => item.EmailInvitationRequestId == ada.EmailInvitationID)
			.HitStatus.Should().Be("Not Clear");

		filtered.Count.Should().Be(1);
		filtered.Data.Should().ContainSingle();
		filtered.Data.Single().Should().BeEquivalentTo(new
		{
			EmailInvitationRequestId = ada.EmailInvitationID,
			SubjectName = "Ada Lovelace",
			OrderStatus = "Completed",
			SelectedPackage = "Basic Screening",
			HitStatus = "Not Clear"
		});
	}

	[Fact]
	public async Task GetReportsAsync_ShouldIsolateAllClientAndRequestorScopes()
	{
		var firstRequestorId = Guid.CreateVersion7();
		var secondRequestorId = Guid.CreateVersion7();
		var thirdRequestorId = Guid.CreateVersion7();
		var first = CreateInvitation("Scope First", "Completed", DateTime.UtcNow.AddHours(-4));
		first.ClientId = 101;
		first.RequestorId = firstRequestorId;
		var second = CreateInvitation("Scope Second", "Completed", DateTime.UtcNow.AddHours(-3));
		second.ClientId = 101;
		second.RequestorId = secondRequestorId;
		var third = CreateInvitation("Scope Third", "Completed", DateTime.UtcNow.AddHours(-2));
		third.ClientId = 202;
		third.RequestorId = thirdRequestorId;
		var legacy = CreateInvitation("Scope Legacy", "Completed", DateTime.UtcNow.AddHours(-1));
		await AddInvitationsAsync(first, second, third, legacy);

		var request = new PaginationRequest(PageIndex: 1, PageSize: 10);
		SetReportScope(AtsRoleIds.PlatformManager, null, firstRequestorId, isPlatformSuperAdmin: true);
		var allReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);

		await _dbContext.UserClientDetails.AddAsync(new UserClientDetails
		{
			UserId = firstRequestorId,
			ClientId = 101
		});
		await _dbContext.SaveChangesAsync();
		SetReportScope(AtsRoleIds.Admin, 101, firstRequestorId);
		var clientReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);

		SetReportScope(AtsRoleIds.User, null, firstRequestorId);
		var requestorReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);

		allReports.Count.Should().Be(4);
		allReports.Data.Select(report => report.EmailInvitationRequestId)
			.Should().BeEquivalentTo([first.EmailInvitationID, second.EmailInvitationID, third.EmailInvitationID, legacy.EmailInvitationID]);
		clientReports.Count.Should().Be(2);
		clientReports.Data.Select(report => report.EmailInvitationRequestId)
			.Should().BeEquivalentTo([first.EmailInvitationID, second.EmailInvitationID]);
		requestorReports.Count.Should().Be(1);
		requestorReports.Data.Should().ContainSingle(report => report.EmailInvitationRequestId == first.EmailInvitationID);
	}

	[Fact]
	public async Task GetReportsAsync_ShouldEnforceSearchReportRoleScopes()
	{
		var userId = Guid.CreateVersion7();
		var uploaderId = Guid.CreateVersion7();
		var adminId = Guid.CreateVersion7();
		var platformManagerId = Guid.CreateVersion7();
		var superAdminId = Guid.CreateVersion7();
		var userFirstClient = CreateInvitation("User First Client", "Completed", DateTime.UtcNow.AddHours(-6));
		userFirstClient.ClientId = 1;
		userFirstClient.RequestorId = userId;
		var userSecondClient = CreateInvitation("User Second Client", "Completed", DateTime.UtcNow.AddHours(-5));
		userSecondClient.ClientId = 2;
		userSecondClient.RequestorId = userId;
		var uploader = CreateInvitation("Uploader", "Completed", DateTime.UtcNow.AddHours(-4));
		uploader.ClientId = 2;
		uploader.RequestorId = uploaderId;
		var adminClient = CreateInvitation("Admin Client", "Completed", DateTime.UtcNow.AddHours(-3));
		adminClient.ClientId = 3;
		adminClient.RequestorId = Guid.CreateVersion7();
		var managerClient = CreateInvitation("Manager Client", "Completed", DateTime.UtcNow.AddHours(-2));
		managerClient.ClientId = 4;
		managerClient.RequestorId = Guid.CreateVersion7();
		var unauthorized = CreateInvitation("Unauthorized", "Completed", DateTime.UtcNow.AddHours(-1));
		unauthorized.ClientId = 5;
		unauthorized.RequestorId = Guid.CreateVersion7();
		await AddInvitationsAsync(
			userFirstClient,
			userSecondClient,
			uploader,
			adminClient,
			managerClient,
			unauthorized);
		await _dbContext.UserClientDetails.AddRangeAsync(
			new UserClientDetails { UserId = adminId, ClientId = 3 },
			new UserClientDetails { UserId = platformManagerId, ClientId = 4 });
		await _dbContext.SaveChangesAsync();
		var request = new PaginationRequest(PageIndex: 1, PageSize: 20);

		SetReportScope(AtsRoleIds.User, 999, userId);
		var userReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);
		SetReportScope(AtsRoleIds.Uploader, 999, uploaderId);
		var uploaderReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);
		SetReportScope(AtsRoleIds.Admin, 999, adminId);
		var adminReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);
		SetReportScope(AtsRoleIds.PlatformManager, 999, platformManagerId);
		var managerReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);
		SetReportScope(AtsRoleIds.User, null, superAdminId, isPlatformSuperAdmin: true);
		var allReports = await _reportService.GetReportsAsync(request, null, false, CancellationToken.None);

		userReports.Data.Select(report => report.EmailInvitationRequestId).Should().BeEquivalentTo(
			[userFirstClient.EmailInvitationID, userSecondClient.EmailInvitationID]);
		uploaderReports.Data.Should().ContainSingle(report => report.EmailInvitationRequestId == uploader.EmailInvitationID);
		adminReports.Data.Should().ContainSingle(report => report.EmailInvitationRequestId == adminClient.EmailInvitationID);
		managerReports.Data.Should().ContainSingle(report => report.EmailInvitationRequestId == managerClient.EmailInvitationID);
		allReports.Count.Should().Be(6);
	}

	[Fact]
	public async Task GetReportResultByEmailInvitationRequestIdAsync_ShouldMapApplicantGraphAndPreferredReport()
	{
		// Arrange
		var invitation = CreateInvitation(
			"Result",
			orderStatus: "Completed",
			orderCompletedAt: new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc));
		invitation.FirstName = "Renzy";
		invitation.LastName = "Gutierrez";
		invitation.FormCompletedAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
		invitation.PersonalDetails = new PersonalDetails
		{
			PersonalID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID,
			ResumeFileName = "resume.pdf",
			ResumeFileKey = "documents/resume.pdf",
			AdditionalGovtIDFileName = "government-id.pdf",
			AdditionalGovtIDFileKey = "documents/government-id.pdf",
			BiometricFileName = "photo.jpg",
			BiometricFileKey = "documents/photo.jpg",
			CreatedDate = DateTime.UtcNow
		};
		invitation.EducationalBackground = new EducationalBackground
		{
			EducationalBackgroundID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID,
			BachelorsDiplomaFileName = "diploma.pdf",
			BachelorsDiplomaFileKey = "documents/diploma.pdf",
			CreatedDate = DateTime.UtcNow
		};
		invitation.ProfessionalExperiences = new ProfessionalExperiences
		{
			ProfessionalExperiencesID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID,
			Emp1COEUploadFileName = "coe.pdf",
			Emp1COEUploadFileKey = "documents/coe.pdf",
			CreatedDate = DateTime.UtcNow
		};
		invitation.SignatureDetails = new SignatureDetails
		{
			SignatureDetailsID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID,
			ConsentFormFileName = "consent.pdf",
			ConsentFormFileKey = "documents/consent.pdf"
		};
		invitation.ReportDetails =
		[
			CreateReport(invitation.EmailInvitationID, "Initial Report", "Clear", "initial.pdf", new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc)),
			CreateReport(invitation.EmailInvitationID, "Supplementary Report", "Not Clear", "supplementary.pdf", new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc))
		];
		await AddInvitationsAsync(invitation);

		// Act
		var result = await _reportService.GetReportResultByEmailInvitationRequestIdAsync(
			invitation.EmailInvitationID,
			CancellationToken.None);

		// Assert
		result.Should().BeEquivalentTo(new
		{
			SubjectName = "Renzy Gutierrez",
			OrderStatus = "Completed",
			HitStatus = "Not Clear",
			SelectedPackage = "Basic Screening",
			ResumeFileName = "resume.pdf",
			ResumeFileKey = "documents/resume.pdf",
			IdUploadedFileName = "government-id.pdf",
			IdUploadedFileKey = "documents/government-id.pdf",
			CoeFileName = "coe.pdf",
			CoeFileKey = "documents/coe.pdf",
			DiplomaFileName = "diploma.pdf",
			DiplomaFileKey = "documents/diploma.pdf",
			BiometricPhotoFileName = "photo.jpg",
			BiometricPhotoFileKey = "documents/photo.jpg",
			ConsentFormFileName = "consent.pdf",
			ConsentFormFileKey = "documents/consent.pdf",
			UploadedReportFileName = "supplementary.pdf",
			UploadedReportFileKey = "reports/supplementary.pdf",
			FilledFormAt = "August 01, 2026",
			ReportUploadedAt = "August 10, 2026"
		});
	}

	[Fact]
	public async Task DownloadIndividualReportAsync_ShouldReturnZipWithStoredDocuments()
	{
		// Arrange
		var resumeKey = await StoreAsync("documents", "resume.txt", "resume-content");
		var idKey = await StoreAsync("documents", "id.txt", "id-content");
		var request = new DownloadIndividualDocumentsRequestDTO
		{
			SubjectName = "Integration Candidate",
			FileDocuments =
			[
				new DownloadIndividualDocuments { FileKey = resumeKey, FileName = "resume.txt" },
				new DownloadIndividualDocuments { FileKey = idKey, FileName = "id.txt" }
			]
		};

		// Act
		await using var result = await _reportService.DownloadIndividualReportAsync(
			request,
			CancellationToken.None);

		// Assert
		using var archive = new ZipArchive(result, ZipArchiveMode.Read);
		archive.Entries.Select(entry => entry.FullName).Should().Equal("resume.txt", "id.txt");
		(await ReadZipEntryAsync(archive.GetEntry("resume.txt")!)).Should().Be("resume-content");
		(await ReadZipEntryAsync(archive.GetEntry("id.txt")!)).Should().Be("id-content");
	}

	[Fact]
	public async Task DownloadMultipleOrderRecordsAsync_ShouldQueryApplicantDocumentsAndMergePdfs()
	{
		// Arrange
		var invitation = CreateInvitation("Download", orderStatus: "Completed");
		invitation.FirstName = "Renzy";
		invitation.LastName = "Gutierrez";

		var pdfBytes = CreatePdfBytes();
		var resumeKey = await StoreAsync("documents", "resume.pdf", pdfBytes);
		var biometricKey = await StoreAsync("documents", "biometric.pdf", pdfBytes);
		invitation.PersonalDetails = new PersonalDetails
		{
			PersonalID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID,
			ResumeFileName = "resume.pdf",
			ResumeFileKey = resumeKey,
			BiometricFileName = "biometric.pdf",
			BiometricFileKey = biometricKey,
			CreatedDate = DateTime.UtcNow
		};
		invitation.EducationalBackground = new EducationalBackground
		{
			EducationalBackgroundID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID,
			CreatedDate = DateTime.UtcNow
		};
		invitation.ProfessionalExperiences = new ProfessionalExperiences
		{
			ProfessionalExperiencesID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID,
			CreatedDate = DateTime.UtcNow
		};
		invitation.SignatureDetails = new SignatureDetails
		{
			SignatureDetailsID = Guid.CreateVersion7(),
			EmailInvitationID = invitation.EmailInvitationID
		};
		await AddInvitationsAsync(invitation);

		var request = new DownloadMultipleOrderRecordsRequestDTO
		{
			EmailInvitaionRequestList = [invitation.EmailInvitationID]
		};

		// Act
		await using var result = await _reportService.DownloadMultipleOrderRecordsAsync(
			request,
			CancellationToken.None);

		// Assert
		using var archive = new ZipArchive(result, ZipArchiveMode.Read);
		archive.Entries.Should().ContainSingle();
		archive.Entries[0].FullName.Should().Be("Renzy_Gutierrez.pdf");

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
			ReportStatus = "Initial Report"
		};

		// Act
		Func<Task> act = () => _reportService.UploadReportAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("Report file is required.");
		(await _dbContext.ReportDetails.AsNoTracking().AnyAsync()).Should().BeFalse();
	}

	[Fact]
	public async Task UploadReportAsync_ShouldThrowNotFoundException_WhenInvitationDoesNotExist()
	{
		// Arrange
		var missingId = Guid.CreateVersion7();
		var request = CreateUploadRequest(
			missingId,
			"Initial Report",
			"Clear",
			"missing-invitation.pdf",
			"report content");

		// Act
		Func<Task> act = () => _reportService.UploadReportAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"Email invitation with ID {missingId} not found.");
		(await _dbContext.ReportDetails.AsNoTracking().AnyAsync()).Should().BeFalse();
	}

	[Fact]
	public async Task UploadReportAsync_ShouldDeleteStoredFile_WhenDatabaseRejectsReport()
	{
		// Arrange
		var invitation = CreateInvitation("Rollback", orderStatus: "Pending Candidate Info");
		await AddInvitationsAsync(invitation);

		const string fileName = "invalid-report.pdf";
		var request = CreateUploadRequest(
			invitation.EmailInvitationID,
			"Initial Report",
			new string('X', 256),
			fileName,
			"invalid report content");
		var expectedFileKey = BuildReportFileKey(fileName);

		// Act
		Func<Task> act = () => _reportService.UploadReportAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to upload report.*");
		_dbContext.ChangeTracker.Clear();
		(await _dbContext.ReportDetails.AsNoTracking().AnyAsync()).Should().BeFalse();

		Func<Task> downloadDeletedFile = async () =>
		{
			await using var stream = await _objectStorageService.DownloadAsync(
				expectedFileKey,
				CancellationToken.None);
		};
		await downloadDeletedFile.Should()
			.ThrowAsync<Exception>()
			.WithMessage($"The specified key does not exist: {expectedFileKey}");
	}

	#endregion

	private string BuildReportFileKey(string fileName) =>
		$"{_configuration["ATS:ATSReportFileFolderName"] ?? string.Empty}/{fileName}";

	private void SetReportScope(
		int atsRoleId,
		int? clientId,
		Guid? userId,
		bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim> { new(AuthClaimTypes.AtsRoleId, atsRoleId.ToString()) };
		if (clientId.HasValue)
			claims.Add(new Claim(AuthClaimTypes.AtsClientId, clientId.Value.ToString()));
		if (userId.HasValue)
			claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
		if (isPlatformSuperAdmin)
			claims.Add(new Claim(AuthClaimTypes.PlatformRoleId, PlatformRoleIds.SuperAdmin.ToString()));

		_httpContextAccessor.HttpContext!.User =
			new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
	}

	private async Task AddInvitationsAsync(params EmailInvitationRequest[] invitations)
	{
		await _dbContext.EmailInvitationRequests.AddRangeAsync(invitations);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}

	private async Task<string> StoreAsync(string folder, string fileName, string content) =>
		await StoreAsync(folder, fileName, Encoding.UTF8.GetBytes(content));

	private async Task<string> StoreAsync(string folder, string fileName, byte[] content)
	{
		await using var stream = new MemoryStream(content);
		return await _objectStorageService.UploadAsync(
			folder,
			fileName,
			stream,
			CancellationToken.None);
	}

	private static ReportDetailsDTO CreateUploadRequest(
		Guid invitationId,
		string reportStatus,
		string hitStatus,
		string fileName,
		string content) => new()
	{
		EmailInvitationRequestId = invitationId,
		HitStatus = hitStatus,
		ReportStatus = reportStatus,
		ReportFile = CreateFormFile(fileName, content)
	};

	private static IFormFile CreateFormFile(string fileName, string content)
	{
		var bytes = Encoding.UTF8.GetBytes(content);
		return new FormFile(
			new MemoryStream(bytes),
			0,
			bytes.Length,
			"ReportFile",
			fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = "application/pdf"
		};
	}

	private static EmailInvitationRequest CreateInvitation(
		string prefix,
		string orderStatus,
		DateTime? orderCompletedAt = null)
	{
		var id = Guid.CreateVersion7();
		var now = DateTime.UtcNow;
		return new EmailInvitationRequest
		{
			EmailInvitationID = id,
			FirstName = $"{prefix} First",
			LastName = $"{prefix} Last",
			MiddleInitial = prefix[..1],
			EmailAddress = $"{prefix.ToLowerInvariant()}@example.com",
			MobileNumber = "+639171234567",
			Requestor = "ATS Integration Tests",
			SelectPackage = "Basic Screening",
			RushNormal = "Normal",
			HashToken = $"hash-{id}",
			HashTokenCreatedAt = now,
			HashTokenExpiration = now.AddDays(1),
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Done",
			OrderStatus = orderStatus,
			OrderCreatedAt = now.AddDays(-2),
			OrderCompletedAt = orderCompletedAt
		};
	}

	private static ReportDetails CreateReport(
		Guid invitationId,
		string reportStatus,
		string hitStatus,
		string fileName,
		DateTime uploadedAt) => new()
	{
		ReportFileId = Guid.CreateVersion7(),
		EmailInvitationRequestId = invitationId,
		HitStatus = hitStatus,
		ReportStatus = reportStatus,
		ReportFileName = fileName,
		ReportFileKey = $"reports/{fileName}",
		ReportUploadedAt = uploadedAt
	};

	private static async Task<string> ReadTextAsync(Stream stream)
	{
		using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
		return await reader.ReadToEndAsync();
	}

	private static async Task<string> ReadZipEntryAsync(ZipArchiveEntry entry)
	{
		await using var stream = entry.Open();
		return await ReadTextAsync(stream);
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
