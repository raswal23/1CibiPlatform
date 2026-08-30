using ATS.Constants;
using ATS.Data.Entities;
using ATS.Data.Repository.PublicApi;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class PublicApiRepositoryIntegrationTests : BaseIntegrationTest
{
	private const int OwningClientId = 8101;
	private const int OtherClientId = 8102;

	// ATS.Constants.OrderStatus and ApplicationFormStatus are internal to the module,
	// so the stored values are used directly here.
	private const string PendingCandidateInfoStatus = "Pending Candidate Info";
	private const string ApplicationWithdrawnStatus = "Application Withdrawn";
	private const string CompletedStatus = "Completed";
	private const string FormPendingStatus = "Pending";
	private const string FormWithdrawnStatus = "Withdrawn";

	private static readonly Guid OwningRequestorId = Guid.CreateVersion7();

	private readonly PublicApiRepository _repository;

	public PublicApiRepositoryIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
		_repository = new PublicApiRepository(_dbContext);
	}

	private async Task<EmailInvitationRequest> SeedOrderAsync(
		int? clientId = OwningClientId,
		Guid? requestorId = null,
		string orderStatus = PendingCandidateInfoStatus,
		string applicationFormStatus = FormPendingStatus)
	{
		var order = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Juan",
			LastName = "Dela Cruz",
			EmailAddress = "juan@example.com",
			MobileNumber = "09171234567",
			SelectPackage = "CRIMINAL RECORDS CHECK",
			RushNormal = "Normal",
			HashToken = Guid.NewGuid().ToString("N"),
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddHours(24),
			EmailSentStatus = "Pending",
			ApplicationFormStatus = applicationFormStatus,
			OrderStatus = orderStatus,
			OrderCreatedAt = DateTime.UtcNow,
			ClientId = clientId,
			RequestorId = requestorId ?? OwningRequestorId
		};

		_dbContext.EmailInvitationRequests.Add(order);
		await _dbContext.SaveChangesAsync();

		return order;
	}

	[Fact]
	public async Task GetOrderAsync_ShouldReturnTheOrder_WhenItBelongsToTheCallersClient()
	{
		var order = await SeedOrderAsync();

		var result = await _repository.GetOrderAsync(
			order.EmailInvitationID,
			[OwningClientId],
			null,
			CancellationToken.None);

		result.Should().NotBeNull();
		result!.OrderId.Should().Be(order.EmailInvitationID);
		result.Package.Should().Be("CRIMINAL RECORDS CHECK");
	}

	[Fact]
	public async Task GetOrderAsync_ShouldReturnNull_WhenTheOrderBelongsToAnotherClient()
	{
		var order = await SeedOrderAsync();

		var result = await _repository.GetOrderAsync(
			order.EmailInvitationID,
			[OtherClientId],
			null,
			CancellationToken.None);

		// Null, not a partial record: the service turns this into a 404 so the API
		// never confirms that another client's order exists.
		result.Should().BeNull();
	}

	[Fact]
	public async Task GetOrderAsync_ShouldReturnNull_WhenTheOrderWasRaisedByAnotherUser()
	{
		var order = await SeedOrderAsync();

		var result = await _repository.GetOrderAsync(
			order.EmailInvitationID,
			[OwningClientId],
			Guid.CreateVersion7(),
			CancellationToken.None);

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetOrderAsync_ShouldReturnAnyOrder_ForAnUnrestrictedCaller()
	{
		var order = await SeedOrderAsync();

		// A null client set means super admin.
		var result = await _repository.GetOrderAsync(
			order.EmailInvitationID,
			null,
			null,
			CancellationToken.None);

		result.Should().NotBeNull();
	}

	[Fact]
	public async Task GetOrderAsync_ShouldIncludeTheOrdersHistory()
	{
		var order = await SeedOrderAsync();

		_dbContext.OrderStatusHistories.Add(new OrderStatusHistory
		{
			OrderStatusHistoryId = Guid.CreateVersion7(),
			EmailInvitationRequestId = order.EmailInvitationID,
			EventType = OrderHistoryEventType.OrderCreated,
			NewStatus = PendingCandidateInfoStatus,
			Source = OrderHistorySource.PublicApi,
			OccurredAt = DateTime.UtcNow
		});

		await _dbContext.SaveChangesAsync();

		var result = await _repository.GetOrderAsync(
			order.EmailInvitationID,
			[OwningClientId],
			null,
			CancellationToken.None);

		result!.History.Should().ContainSingle();
		result.History[0].Source.Should().Be(OrderHistorySource.PublicApi);
	}

	[Fact]
	public async Task WithdrawOrderAsync_ShouldWithdrawTheCallersOwnOrder()
	{
		var order = await SeedOrderAsync();

		var withdrawn = await _repository.WithdrawOrderAsync(
			order.EmailInvitationID,
			[OwningClientId],
			null,
			CancellationToken.None);

		withdrawn.Should().BeTrue();

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.ApplicationFormStatus.Should().Be(FormWithdrawnStatus);
		saved.OrderStatus.Should().Be(ApplicationWithdrawnStatus);

		// The denormalized search row has to be rebuilt with the new status.
		saved.NeedsProjection.Should().BeTrue();
	}

	[Fact]
	public async Task WithdrawOrderAsync_ShouldRefuse_WhenTheOrderBelongsToAnotherClient()
	{
		var order = await SeedOrderAsync();

		var withdrawn = await _repository.WithdrawOrderAsync(
			order.EmailInvitationID,
			[OtherClientId],
			null,
			CancellationToken.None);

		withdrawn.Should().BeFalse();

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.OrderStatus.Should().Be(PendingCandidateInfoStatus);
	}

	[Fact]
	public async Task WithdrawOrderAsync_ShouldRefuse_WhenTheOrderIsAlreadyWithdrawn()
	{
		var order = await SeedOrderAsync(
			applicationFormStatus: FormWithdrawnStatus,
			orderStatus: ApplicationWithdrawnStatus);

		var withdrawn = await _repository.WithdrawOrderAsync(
			order.EmailInvitationID,
			[OwningClientId],
			null,
			CancellationToken.None);

		// The service turns this into a 409 rather than reporting a silent success.
		withdrawn.Should().BeFalse();
	}

	[Fact]
	public async Task WithdrawOrderAsync_ShouldRefuse_WhenTheOrderIsAlreadyCompleted()
	{
		var order = await SeedOrderAsync(orderStatus: CompletedStatus);

		var withdrawn = await _repository.WithdrawOrderAsync(
			order.EmailInvitationID,
			[OwningClientId],
			null,
			CancellationToken.None);

		withdrawn.Should().BeFalse();
	}

	[Fact]
	public async Task WithdrawOrderAsync_ShouldBeIdempotent_WhenCalledTwice()
	{
		var order = await SeedOrderAsync();

		var first = await _repository.WithdrawOrderAsync(
			order.EmailInvitationID, [OwningClientId], null, CancellationToken.None);

		var second = await _repository.WithdrawOrderAsync(
			order.EmailInvitationID, [OwningClientId], null, CancellationToken.None);

		// Two simultaneous calls must not both report success.
		first.Should().BeTrue();
		second.Should().BeFalse();
	}

	[Fact]
	public async Task GetBulkUploadStatusAsync_ShouldReturnTheParseOutcome()
	{
		var fileId = Guid.CreateVersion7();

		_dbContext.BulkUploadFileDetails.Add(new BulkUploadFileDetails
		{
			FileID = fileId,
			FileName = "subjects.csv",
			FileKey = "test/subjects.csv",
			PackageType = "CRIMINAL RECORDS CHECK",
			OrderType = "Normal",
			Status = BulkFileStatus.Done,
			ClientId = OwningClientId,
			UploadedByUserId = OwningRequestorId,
			DateCreated = DateTime.UtcNow,
			Source = OrderHistorySource.PublicApi,
			AcceptedRowCount = 24,
			RejectedRowCount = 1,
			RejectedRows = """[{"RowNumber":7,"Reason":"Mobile number must be 11 digits."}]"""
		});

		await _dbContext.SaveChangesAsync();

		var result = await _repository.GetBulkUploadStatusAsync(
			fileId,
			[OwningClientId],
			null,
			CancellationToken.None);

		result.Should().NotBeNull();
		result!.AcceptedRowCount.Should().Be(24);
		result.RejectedRowCount.Should().Be(1);

		// The rejected rows never became entities, so they are read back from the
		// stored JSON rather than from a table.
		result.RejectedRows.Should().ContainSingle();
		result.RejectedRows[0].RowNumber.Should().Be(7);
	}

	[Fact]
	public async Task GetBulkUploadStatusAsync_ShouldReturnNull_ForAnotherClientsUpload()
	{
		var fileId = Guid.CreateVersion7();

		_dbContext.BulkUploadFileDetails.Add(new BulkUploadFileDetails
		{
			FileID = fileId,
			FileName = "subjects.csv",
			FileKey = "test/subjects.csv",
			PackageType = "CRIMINAL RECORDS CHECK",
			OrderType = "Normal",
			Status = BulkFileStatus.Pending,
			ClientId = OwningClientId,
			UploadedByUserId = OwningRequestorId,
			DateCreated = DateTime.UtcNow
		});

		await _dbContext.SaveChangesAsync();

		var result = await _repository.GetBulkUploadStatusAsync(
			fileId,
			[OtherClientId],
			null,
			CancellationToken.None);

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetBulkUploadStatusAsync_ShouldReturnNoRejects_WhenNoneWereRecorded()
	{
		var fileId = Guid.CreateVersion7();

		_dbContext.BulkUploadFileDetails.Add(new BulkUploadFileDetails
		{
			FileID = fileId,
			FileName = "clean.csv",
			FileKey = "test/clean.csv",
			PackageType = "CRIMINAL RECORDS CHECK",
			OrderType = "Normal",
			Status = BulkFileStatus.Done,
			ClientId = OwningClientId,
			UploadedByUserId = OwningRequestorId,
			DateCreated = DateTime.UtcNow,
			AcceptedRowCount = 10,
			RejectedRowCount = 0,
			RejectedRows = null
		});

		await _dbContext.SaveChangesAsync();

		var result = await _repository.GetBulkUploadStatusAsync(
			fileId,
			[OwningClientId],
			null,
			CancellationToken.None);

		// Null in the column reads as an empty list, not a deserialization failure.
		result!.RejectedRows.Should().BeEmpty();
	}
}
