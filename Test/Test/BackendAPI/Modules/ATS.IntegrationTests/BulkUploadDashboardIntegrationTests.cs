using ATS.Constants;
using ATS.Data.Entities;
using Auth.Constants;
using BuildingBlocks.Pagination;
using FluentAssertions;
using System.Security.Claims;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class BulkUploadDashboardIntegrationTests : BaseIntegrationTest
{
	private const int ClientA = 1;
	private const int ClientB = 2;

	// ATS.Constants.EmailStatus and OrderStatus are internal to the ATS assembly, so the
	// wire values are restated here the same way the neighbouring ATS tests do.
	private const string EmailSentPending = "Pending";
	private const string EmailSentDone = "Done";
	private const string EmailSentError = "Error";
	private const string PendingCandidateInfo = "Pending Candidate Info";

	private static readonly Guid UploaderId = Guid.CreateVersion7();
	private static readonly Guid OtherUploaderId = Guid.CreateVersion7();
	private static readonly Guid AdminId = Guid.CreateVersion7();

	// A fixed anchor keeps the expected keyset order deterministic across runs.
	private static readonly DateTime Anchor = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

	public BulkUploadDashboardIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Scope

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnOnlyOwnFiles_WhenCallerIsUploader()
	{
		await AddFilesAsync(
			NewFile("mine-1.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor),
			NewFile("mine-2.csv", UploaderId, ClientA, BulkFileStatus.Pending, Anchor.AddMinutes(-1)),
			NewFile("theirs.csv", OtherUploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-2)));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50),
			status: null,
			CancellationToken.None);

		result.Items.Select(item => item.FileName)
			.Should().BeEquivalentTo(["mine-1.csv", "mine-2.csv"]);
		result.TotalCount.Should().Be(2);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnEveryUploaderWithinAssignedClient_WhenCallerIsAdmin()
	{
		await AddFilesAsync(
			NewFile("a-mine.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor),
			NewFile("a-theirs.csv", OtherUploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-1)),
			NewFile("b-other-client.csv", UploaderId, ClientB, BulkFileStatus.Done, Anchor.AddMinutes(-2)));

		await AddAssignmentAsync(AdminId, ClientA);
		SetAuthenticatedUser(AdminId, AtsRoleIds.Admin, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50),
			status: null,
			CancellationToken.None);

		result.Items.Select(item => item.FileName)
			.Should().BeEquivalentTo(["a-mine.csv", "a-theirs.csv"]);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnEmpty_WhenAdminHasNoClientAssignments()
	{
		await AddFilesAsync(NewFile("orphan.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor));

		// No AddAssignmentAsync: an empty client set filters everything out, which is
		// deliberately different from the null set a super admin gets.
		SetAuthenticatedUser(AdminId, AtsRoleIds.Admin, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50),
			status: null,
			CancellationToken.None);

		result.Items.Should().BeEmpty();
		result.TotalCount.Should().Be(0);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnEveryClient_WhenCallerIsPlatformSuperAdmin()
	{
		await AddFilesAsync(
			NewFile("client-a.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor),
			NewFile("client-b.csv", OtherUploaderId, ClientB, BulkFileStatus.Pending, Anchor.AddMinutes(-1)));

		SetAuthenticatedUser(AdminId, AtsRoleIds.Admin, ClientA, isPlatformSuperAdmin: true);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50),
			status: null,
			CancellationToken.None);

		result.Items.Select(item => item.FileName)
			.Should().BeEquivalentTo(["client-a.csv", "client-b.csv"]);
	}

	#endregion

	#region Status filter and counts

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnOnlyMatchingStatus_WhenStatusIsSupplied()
	{
		await AddFilesAsync(
			NewFile("pending.csv", UploaderId, ClientA, BulkFileStatus.Pending, Anchor),
			NewFile("processing.csv", UploaderId, ClientA, BulkFileStatus.Processing, Anchor.AddMinutes(-1)),
			NewFile("done.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-2)));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50),
			BulkFileStatus.Processing,
			CancellationToken.None);

		result.Items.Single().FileName.Should().Be("processing.csv");
		result.TotalCount.Should().Be(1);
	}

	[Fact]
	public async Task GetStatusCountsAsync_ShouldCountEveryBucket_WhenFilesSpanAllStatuses()
	{
		await AddFilesAsync(
			NewFile("p1.csv", UploaderId, ClientA, BulkFileStatus.Pending, Anchor),
			NewFile("p2.csv", UploaderId, ClientA, BulkFileStatus.Pending, Anchor.AddMinutes(-1)),
			NewFile("proc.csv", UploaderId, ClientA, BulkFileStatus.Processing, Anchor.AddMinutes(-2)),
			NewFile("d1.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-3)),
			NewFile("hidden.csv", OtherUploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-4)));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var counts = await _bulkUploadMonitoringService.GetStatusCountsAsync(
			null,
			null,
			null,
			CancellationToken.None);

		counts.Pending.Should().Be(2);
		counts.Processing.Should().Be(1);
		counts.Done.Should().Be(1);

		// The other uploader's file is excluded by scope, so Total is 4, not 5.
		counts.Total.Should().Be(4);
	}

	[Fact]
	public async Task GetStatusCountsAsync_ShouldHonourSearchButNotSelectedStatus()
	{
		await AddFilesAsync(
			NewFile("payroll-pending.csv", UploaderId, ClientA, BulkFileStatus.Pending, Anchor),
			NewFile("payroll-done.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-1)),
			NewFile("unrelated.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-2)));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var counts = await _bulkUploadMonitoringService.GetStatusCountsAsync(
			"payroll",
			null,
			null,
			CancellationToken.None);

		// Both buckets keep reporting their own size so the chips stay usable while one
		// of them is selected.
		counts.Pending.Should().Be(1);
		counts.Done.Should().Be(1);
		counts.Total.Should().Be(2);
	}

	#endregion

	#region Search and date range

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldMatchCaseInsensitively_WhenSearchTermIsSupplied()
	{
		await AddFilesAsync(
			NewFile("Quarterly-Hires.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor),
			NewFile("unrelated.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor.AddMinutes(-1)));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50, SearchTerm: "quarterly"),
			status: null,
			CancellationToken.None);

		result.Items.Single().FileName.Should().Be("Quarterly-Hires.csv");
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldIncludeTheEndDate_WhenDateRangeIsSupplied()
	{
		var endDay = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);

		await AddFilesAsync(
			// Late on the end day: must be included, so the range is exclusive of the
			// following midnight rather than of the end day itself.
			NewFile("in-range.csv", UploaderId, ClientA, BulkFileStatus.Done, endDay.AddHours(23)),
			NewFile("after-range.csv", UploaderId, ClientA, BulkFileStatus.Done, endDay.AddDays(1)));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50, StartDate: endDay, EndDate: endDay),
			status: null,
			CancellationToken.None);

		result.Items.Single().FileName.Should().Be("in-range.csv");
	}

	#endregion

	#region Keyset walk

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldWalkEveryRowExactlyOnce_WhenPagingForward()
	{
		var files = Enumerable.Range(0, 25)
			.Select(index => NewFile(
				$"file-{index:D2}.csv",
				UploaderId,
				ClientA,
				BulkFileStatus.Done,
				Anchor.AddMinutes(-index)))
			.ToArray();

		await AddFilesAsync(files);

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var seen = new List<string>();
		string? cursor = null;
		long? firstPageTotal = null;

		for (var page = 0; page < 20; page++)
		{
			var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
				new KeysetPaginationRequest(Cursor: cursor, PageSize: 7),
				status: null,
				CancellationToken.None);

			firstPageTotal ??= result.TotalCount;
			seen.AddRange(result.Items.Select(item => item.FileName!));

			cursor = result.NextCursor;
			if (cursor is null)
			{
				break;
			}
		}

		cursor.Should().BeNull("the walk must terminate");
		seen.Should().HaveCount(25);
		seen.Should().OnlyHaveUniqueItems();

		// Newest upload first, which is the fixed (DateCreated DESC, FileID ASC) order.
		seen.Should().BeInAscendingOrder();
		firstPageTotal.Should().Be(25);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnFirstPage_WhenCursorIsMalformed()
	{
		await AddFilesAsync(NewFile("only.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(Cursor: "not-a-real-cursor", PageSize: 10),
			status: null,
			CancellationToken.None);

		result.Items.Single().FileName.Should().Be("only.csv");
		result.TotalCount.Should().Be(1);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldOrderByFileId_WhenUploadTimestampsTie()
	{
		// Two files uploaded in the same instant: only the FileID tiebreaker keeps the
		// walk from skipping or repeating one of them.
		await AddFilesAsync(
			NewFile("tie-a.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor),
			NewFile("tie-b.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor),
			NewFile("tie-c.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var seen = new List<string>();
		string? cursor = null;

		for (var page = 0; page < 5; page++)
		{
			var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
				new KeysetPaginationRequest(Cursor: cursor, PageSize: 1),
				status: null,
				CancellationToken.None);

			seen.AddRange(result.Items.Select(item => item.FileName!));

			cursor = result.NextCursor;
			if (cursor is null)
			{
				break;
			}
		}

		seen.Should().HaveCount(3);
		seen.Should().OnlyHaveUniqueItems();
	}

	#endregion

	#region Invitation rollup

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldRollUpInvitationProgress_WhenFileProducedSubjects()
	{
		var parsed = NewFile("parsed.csv", UploaderId, ClientA, BulkFileStatus.Done, Anchor);
		var unparsed = NewFile("unparsed.csv", UploaderId, ClientA, BulkFileStatus.Pending, Anchor.AddMinutes(-1));

		await AddFilesAsync(parsed, unparsed);

		await AddInvitationsAsync(
			NewInvitation(parsed.FileID, ClientA, UploaderId, EmailSentDone),
			NewInvitation(parsed.FileID, ClientA, UploaderId, EmailSentDone),
			NewInvitation(parsed.FileID, ClientA, UploaderId, EmailSentError),
			NewInvitation(parsed.FileID, ClientA, UploaderId, EmailSentPending),

			// Belongs to no bulk file: a single inquiry must never inflate a rollup.
			NewInvitation(null, ClientA, UploaderId, EmailSentDone));

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var result = await _bulkUploadMonitoringService.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 50),
			status: null,
			CancellationToken.None);

		var parsedRow = result.Items.Single(item => item.FileID == parsed.FileID);
		parsedRow.SubjectCount.Should().Be(4);
		parsedRow.EmailsSent.Should().Be(2);
		parsedRow.EmailsFailed.Should().Be(1);

		var unparsedRow = result.Items.Single(item => item.FileID == unparsed.FileID);
		unparsedRow.SubjectCount.Should().Be(0);
		unparsedRow.EmailsSent.Should().Be(0);
		unparsedRow.EmailsFailed.Should().Be(0);
	}

	#endregion

	#region Helpers

	private async Task AddFilesAsync(params BulkUploadFileDetails[] files)
	{
		await _dbContext.BulkUploadFileDetails.AddRangeAsync(files);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}

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

	private static BulkUploadFileDetails NewFile(
		string fileName,
		Guid uploadedByUserId,
		int clientId,
		string status,
		DateTime dateCreated) => new()
		{
			FileID = Guid.CreateVersion7(),
			UploadedByUserId = uploadedByUserId,
			Requestor = "Integration Test",
			ClientId = clientId,
			FileName = fileName,
			FileKey = $"bulk/{fileName}",
			PackageType = "Standard Screening",
			OrderType = "Normal",
			Status = status,
			DateCreated = dateCreated
		};

	private static EmailInvitationRequest NewInvitation(
		Guid? bulkFileId,
		int clientId,
		Guid requestorId,
		string emailSentStatus)
	{
		var invitationId = Guid.CreateVersion7();
		var now = DateTime.UtcNow;

		return new EmailInvitationRequest
		{
			EmailInvitationID = invitationId,
			FirstName = "Test",
			LastName = "Subject",
			EmailAddress = "subject@example.com",
			MobileNumber = "09171234567",
			Requestor = "Integration Test",
			SelectPackage = "Standard Screening",
			RushNormal = "Normal",
			ClientId = clientId,
			RequestorId = requestorId,
			BulkFileID = bulkFileId,

			// Required columns: the real flow mints the token when the invitation is
			// created. None of them affect the rollup, they only satisfy the schema.
			HashToken = invitationId.ToString("N"),
			HashTokenCreatedAt = now,
			HashTokenExpiration = now.AddDays(7),
			ApplicationFormStatus = "Pending",
			EmailSentStatus = emailSentStatus,
			OrderStatus = PendingCandidateInfo,
			OrderCreatedAt = now
		};
	}

	#endregion
}
