using ATS.Constants;
using ATS.Data.DTO;
using ATS.Data.Repository.BulkUploads;
using ATS.Services.AccessScope;
using ATS.Services.BulkUploadMonitoring;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Globalization;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class BulkUploadMonitoringServiceTests
{
	private const int ClientId = 42;
	private static readonly Guid UploaderId = Guid.CreateVersion7();

	private readonly Mock<IBulkUploadRepository> _repository = new();
	private readonly Mock<IAtsAccessScopeResolver> _scopeResolver = new();
	private readonly BulkUploadMonitoringService _service;

	public BulkUploadMonitoringServiceTests()
	{
		_scopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AtsAccessScope([ClientId], UploaderId));

		_repository
			.Setup(repository => repository.CountBulkUploadsAsync(
				It.IsAny<string?>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(7);

		_repository
			.Setup(repository => repository.GetInvitationRollupAsync(
				It.IsAny<IReadOnlyCollection<Guid>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		_service = new BulkUploadMonitoringService(
			NullLogger<BulkUploadMonitoringService>.Instance,
			_repository.Object,
			_scopeResolver.Object);
	}

	#region Scope

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnEmpty_WhenScopeIsDenied()
	{
		_scopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync((AtsAccessScope?)null);

		var result = await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(),
			status: null,
			CancellationToken.None);

		result.Items.Should().BeEmpty();
		result.NextCursor.Should().BeNull();
		result.TotalCount.Should().Be(0);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				It.IsAny<DateTime?>(),
				It.IsAny<Guid?>(),
				It.IsAny<int>(),
				It.IsAny<string?>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldPassOwnerScopeToRepository_WhenCallerIsUploader()
	{
		SetupPage([]);

		await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(),
			status: null,
			CancellationToken.None);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				null,
				null,
				It.IsAny<int>(),
				null,
				null,
				null,
				null,
				It.Is<IReadOnlyCollection<int>>(clientIds => clientIds.Single() == ClientId),
				UploaderId,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldPassUnrestrictedScope_WhenCallerIsSuperAdmin()
	{
		_scopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new AtsAccessScope(null, null));

		SetupPage([]);

		await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(),
			status: null,
			CancellationToken.None);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				null,
				null,
				It.IsAny<int>(),
				null,
				null,
				null,
				null,
				null,
				null,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task GetStatusCountsAsync_ShouldReturnZeroes_WhenScopeIsDenied()
	{
		_scopeResolver
			.Setup(resolver => resolver.ResolveAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync((AtsAccessScope?)null);

		var counts = await _service.GetStatusCountsAsync(null, null, null, CancellationToken.None);

		counts.Pending.Should().Be(0);
		counts.Processing.Should().Be(0);
		counts.Done.Should().Be(0);
		counts.Total.Should().Be(0);
	}

	#endregion

	#region Status filter

	[Theory]
	[InlineData("pending", BulkFileStatus.Pending)]
	[InlineData("PROCESSING", BulkFileStatus.Processing)]
	[InlineData("Done", BulkFileStatus.Done)]
	public async Task GetBulkUploadsAsync_ShouldNormalizeStatus_WhenCasingDiffers(
		string supplied,
		string expected)
	{
		SetupPage([]);

		await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(),
			supplied,
			CancellationToken.None);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				It.IsAny<DateTime?>(),
				It.IsAny<Guid?>(),
				It.IsAny<int>(),
				expected,
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Archived")]
	public async Task GetBulkUploadsAsync_ShouldTreatStatusAsAll_WhenBlankOrUnknown(string? supplied)
	{
		SetupPage([]);

		await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(),
			supplied,
			CancellationToken.None);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				It.IsAny<DateTime?>(),
				It.IsAny<Guid?>(),
				It.IsAny<int>(),
				null,
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	#endregion

	#region Keyset

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnNextCursor_WhenMoreRowsExist()
	{
		var last = NewRow(DateTime.UtcNow.AddMinutes(-5));

		// pageSize + 1 rows means a next page exists; the sentinel must be trimmed off.
		SetupPage([NewRow(DateTime.UtcNow), last, NewRow(DateTime.UtcNow.AddMinutes(-9))]);

		var result = await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 2),
			status: null,
			CancellationToken.None);

		result.Items.Should().HaveCount(2);
		result.NextCursor.Should().NotBeNull();

		var fields = CursorCodec.Decode(result.NextCursor, 2);
		fields.Should().NotBeNull();
		fields![0].Should().Be(last.DateCreated.ToString("O", CultureInfo.InvariantCulture));
		fields[1].Should().Be(last.FileID.ToString("D"));
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldReturnNullCursor_WhenLastPage()
	{
		SetupPage([NewRow(DateTime.UtcNow)]);

		var result = await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 10),
			status: null,
			CancellationToken.None);

		result.NextCursor.Should().BeNull();
		result.TotalCount.Should().Be(7);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldApplySeek_WhenCursorIsValid()
	{
		var afterDate = new DateTime(2026, 8, 20, 10, 30, 0, DateTimeKind.Utc);
		var afterId = Guid.CreateVersion7();
		var cursor = CursorCodec.Encode(
			afterDate.ToString("O", CultureInfo.InvariantCulture),
			afterId.ToString("D"));

		SetupPage([]);

		var result = await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(Cursor: cursor),
			status: null,
			CancellationToken.None);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				afterDate,
				afterId,
				It.IsAny<int>(),
				It.IsAny<string?>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Once);

		// Cursor pages reuse page one's count rather than recounting.
		result.TotalCount.Should().BeNull();

		_repository.Verify(
			repository => repository.CountBulkUploadsAsync(
				It.IsAny<string?>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Theory]
	[InlineData("not-base64!!")]
	[InlineData("dG9vLWZldy1maWVsZHM=")]
	public async Task GetBulkUploadsAsync_ShouldFallBackToFirstPage_WhenCursorIsUndecodable(string cursor)
	{
		SetupPage([]);

		var result = await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(Cursor: cursor),
			status: null,
			CancellationToken.None);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				null,
				null,
				It.IsAny<int>(),
				It.IsAny<string?>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Once);

		// A bad cursor is a first page, not an error, so the count is computed.
		result.TotalCount.Should().Be(7);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldClampPageSize_WhenAboveMaximum()
	{
		SetupPage([]);

		await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 5000),
			status: null,
			CancellationToken.None);

		_repository.Verify(
			repository => repository.GetBulkUploadsPageAsync(
				It.IsAny<DateTime?>(),
				It.IsAny<Guid?>(),
				KeysetPage.MaxPageSize + 1,
				It.IsAny<string?>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	#endregion

	#region Rollup

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldMergeRollup_WhenInvitationsExist()
	{
		var withInvitations = NewRow(DateTime.UtcNow);
		var withoutInvitations = NewRow(DateTime.UtcNow.AddMinutes(-1));

		SetupPage([withInvitations, withoutInvitations]);

		_repository
			.Setup(repository => repository.GetInvitationRollupAsync(
				It.IsAny<IReadOnlyCollection<Guid>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				new BulkFileInvitationRollupDTO
				{
					FileID = withInvitations.FileID,
					SubjectCount = 120,
					EmailsSent = 118,
					EmailsFailed = 2
				}
			]);

		var result = await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(PageSize: 10),
			status: null,
			CancellationToken.None);

		var merged = result.Items.Single(item => item.FileID == withInvitations.FileID);
		merged.SubjectCount.Should().Be(120);
		merged.EmailsSent.Should().Be(118);
		merged.EmailsFailed.Should().Be(2);

		// A file the bulk job has not parsed yet has no invitations at all.
		var unparsed = result.Items.Single(item => item.FileID == withoutInvitations.FileID);
		unparsed.SubjectCount.Should().Be(0);
		unparsed.EmailsSent.Should().Be(0);
		unparsed.EmailsFailed.Should().Be(0);
	}

	[Fact]
	public async Task GetBulkUploadsAsync_ShouldSkipRollupQuery_WhenPageIsEmpty()
	{
		SetupPage([]);

		var result = await _service.GetBulkUploadsAsync(
			new KeysetPaginationRequest(),
			status: null,
			CancellationToken.None);

		result.Items.Should().BeEmpty();

		_repository.Verify(
			repository => repository.GetInvitationRollupAsync(
				It.IsAny<IReadOnlyCollection<Guid>>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	#endregion

	private void SetupPage(List<BulkUploadRowDTO> rows) =>
		_repository
			.Setup(repository => repository.GetBulkUploadsPageAsync(
				It.IsAny<DateTime?>(),
				It.IsAny<Guid?>(),
				It.IsAny<int>(),
				It.IsAny<string?>(),
				It.IsAny<string?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<DateTime?>(),
				It.IsAny<IReadOnlyCollection<int>?>(),
				It.IsAny<Guid?>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(rows);

	private static BulkUploadRowDTO NewRow(DateTime dateCreated) => new()
	{
		FileID = Guid.CreateVersion7(),
		FileName = "batch.csv",
		Requestor = "J. Cruz",
		PackageType = "Standard Screening",
		OrderType = "Normal",
		Status = BulkFileStatus.Done,
		DateCreated = dateCreated
	};
}
