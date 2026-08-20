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
		var userId = Guid.CreateVersion7();
		const int clientId = 7;
		SetAuthenticatedUser(userId, AtsRoleIds.User, clientId);
		var ada = CreateInvitation(
			"Ada",
			orderStatus: "Completed",
			orderCompletedAt: new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc));
		ada.FirstName = "Ada";
		ada.LastName = "Lovelace";
		ada.ClientId = clientId;
		ada.RequestorId = userId;
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
		grace.ClientId = clientId;
		grace.RequestorId = userId;

		var pending = CreateInvitation("Pending", orderStatus: "In Progress");
		pending.ClientId = clientId;
		pending.RequestorId = userId;
		await AddInvitationsAsync(ada, grace, pending);

		// Act
		var unfiltered = await _reportService.GetReportsAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);
		var filtered = await _reportService.GetReportsAsync(
			new KeysetPaginationRequest(
				Cursor: null,
				PageSize: 10,
				SearchTerm: "ada lovelace",
				StartDate: new DateTime(2026, 8, 1),
				EndDate: new DateTime(2026, 8, 31)),
			CancellationToken.None);

		// Assert
		unfiltered.TotalCount.Should().Be(3);
		unfiltered.Items.Should().HaveCount(3);
		unfiltered.Items.First().EmailInvitationRequestId.Should().Be(grace.EmailInvitationID);
		unfiltered.Items.Single(item => item.EmailInvitationRequestId == ada.EmailInvitationID)
			.HitStatus.Should().Be("Not Clear");

		filtered.TotalCount.Should().Be(1);
		filtered.Items.Should().ContainSingle();
		filtered.Items.Single().Should().BeEquivalentTo(new
		{
			EmailInvitationRequestId = ada.EmailInvitationID,
			SubjectName = "Ada Lovelace",
			OrderStatus = "Completed",
			SelectedPackage = "Basic Screening",
			HitStatus = "Not Clear"
		});
	}

	[Theory]
	[InlineData(AtsRoleIds.PlatformManager)]
	[InlineData(AtsRoleIds.Admin)]
	public async Task GetReportsAsync_ShouldIncludeAllRequestersForAssignedClients(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		var assignedRequesterId = Guid.CreateVersion7();
		var assigned = CreateInvitation("Assigned", orderStatus: "Completed");
		assigned.ClientId = 3;
		assigned.RequestorId = assignedRequesterId;
		var sameClient = CreateInvitation("Same Client", orderStatus: "Completed");
		sameClient.ClientId = 3;
		sameClient.RequestorId = Guid.CreateVersion7();
		var unassigned = CreateInvitation("Unassigned", orderStatus: "Completed");
		unassigned.ClientId = 4;
		unassigned.RequestorId = userId;
		await AddInvitationsAsync(assigned, sameClient, unassigned);
		await AddAssignmentAsync(userId, clientId: 3);
		SetAuthenticatedUser(userId, roleId, claimedClientId: 99);

		var result = await _reportService.GetReportsAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(2);
		result.Items.Select(report => report.EmailInvitationRequestId)
			.Should().BeEquivalentTo(new[]
			{
				assigned.EmailInvitationID,
				sameClient.EmailInvitationID
			});
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetReportsAsync_ShouldRequireOwnRequestorAndClientForRestrictedRoles(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		var matching = CreateInvitation("Matching", orderStatus: "Completed");
		matching.ClientId = 5;
		matching.RequestorId = userId;
		var wrongRequester = CreateInvitation("Wrong Requester", orderStatus: "Completed");
		wrongRequester.ClientId = 5;
		wrongRequester.RequestorId = Guid.CreateVersion7();
		var wrongClient = CreateInvitation("Wrong Client", orderStatus: "Completed");
		wrongClient.ClientId = 6;
		wrongClient.RequestorId = userId;
		await AddInvitationsAsync(matching, wrongRequester, wrongClient);
		SetAuthenticatedUser(userId, roleId, claimedClientId: 5);

		var result = await _reportService.GetReportsAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(1);
		result.Items.Should().ContainSingle()
			.Which.EmailInvitationRequestId.Should().Be(matching.EmailInvitationID);
	}

	[Fact]
	public async Task GetReportsAsync_ShouldIncludeAllClientsAndRequesters_ForPlatformSuperAdmin()
	{
		var first = CreateInvitation("First Client", orderStatus: "Completed");
		first.ClientId = 1;
		first.RequestorId = Guid.CreateVersion7();
		var second = CreateInvitation("Second Client", orderStatus: "Completed");
		second.ClientId = 2;
		second.RequestorId = Guid.CreateVersion7();
		await AddInvitationsAsync(first, second);
		SetAuthenticatedUser(
			Guid.CreateVersion7(),
			AtsRoleIds.User,
			claimedClientId: 99,
			isPlatformSuperAdmin: true);

		var result = await _reportService.GetReportsAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(2);
		result.Items.Select(report => report.EmailInvitationRequestId)
			.Should().BeEquivalentTo(new[] { first.EmailInvitationID, second.EmailInvitationID });
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

	private async Task AddInvitationsAsync(params EmailInvitationRequest[] invitations)
	{
		await _dbContext.EmailInvitationRequests.AddRangeAsync(invitations);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}

	private async Task AddAssignmentAsync(Guid userId, int clientId)
	{
		var now = DateTime.UtcNow;
		await _dbContext.UserClientDetails.AddAsync(new UserClientDetails
		{
			UserId = userId,
			ClientId = clientId,
			CreatedAt = now,
			UpdatedAt = now
		});
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}

	private void SetAuthenticatedUser(
		Guid userId,
		int roleId,
		int claimedClientId,
		bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString()),
			new(AuthClaimTypes.AtsClientId, claimedClientId.ToString())
		};
		if (isPlatformSuperAdmin)
		{
			claims.Add(new Claim(
				AuthClaimTypes.PlatformRoleId,
				PlatformRoleIds.SuperAdmin.ToString()));
		}

		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(claims, "TestAuth"));
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
