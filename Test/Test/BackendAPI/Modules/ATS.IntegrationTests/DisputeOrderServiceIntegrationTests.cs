using System.Security.Claims;
using ATS.Data.Entities;
using ATS.Data.Repository.Administration.Users;
using ATS.DTO;
using ATS.Services;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using BuildingBlocks.SharedServices.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class DisputeOrderServiceIntegrationTests : BaseIntegrationTest
{
	private const string CompanyName = "Integration Test Company";
	private static readonly Guid AuthenticatedUserId = Guid.CreateVersion7();

	public DisputeOrderServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldReturnEligibleOrdersInDisputePriorityOrder()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var disputed = CreateOrder(
			"Disputed",
			"Candidate",
			"disputed@example.com",
			now.AddDays(-10),
			now.AddDays(-9),
			"Report");
		var newest = CreateOrder(
			"Newest",
			"Candidate",
			"newest@example.com",
			now.AddDays(-2),
			now.AddDays(-1));
		var oldest = CreateOrder(
			"Oldest",
			"Candidate",
			"oldest@example.com",
			now.AddDays(-20),
			now.AddDays(-19));
		var outsideDisputeWindow = CreateOrder(
			"Expired",
			"Candidate",
			"expired@example.com",
			now.AddDays(-40),
			now.AddDays(-31));
		var incomplete = CreateOrder(
			"Incomplete",
			"Candidate",
			"incomplete@example.com",
			now.AddDays(-2),
			now.AddDays(-1),
			orderStatus: "In Progress");

		await AddOrdersAsync(disputed, newest, oldest, outsideDisputeWindow, incomplete);
		var service = CreateService(CreateSuccessfulEmailService());

		// Act
		var result = await service.GetDisputeOrdersAsync(
			new PaginationRequest(PageIndex: 1, PageSize: 2),
			CancellationToken.None);

		// Assert
		result.PageIndex.Should().Be(1);
		result.PageSize.Should().Be(2);
		result.Count.Should().Be(3);

		var orders = result.Data.ToArray();
		orders.Select(order => order.EmailInvitationID)
			.Should().Equal(disputed.EmailInvitationID, newest.EmailInvitationID);
		orders[0].Should().BeEquivalentTo(new
		{
			disputed.EmailInvitationID,
			disputed.FirstName,
			disputed.LastName,
			disputed.DisputeCategory
		});
		orders[0].OrderCreatedAt.Should().BeCloseTo(
			disputed.OrderCreatedAt!.Value,
			TimeSpan.FromMilliseconds(1));
		orders[0].OrderCompletedAt.Should().BeCloseTo(
			disputed.OrderCompletedAt!.Value,
			TimeSpan.FromMilliseconds(1));
	}

	[Fact]
	public async Task GetDisputeOrdersAsync_ShouldSearchEligibleOrdersCaseInsensitively()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var firstNameMatch = CreateOrder(
			"Needle",
			"First",
			"first@example.com",
			now.AddDays(-5),
			now.AddDays(-4));
		var lastNameMatch = CreateOrder(
			"Second",
			"NEEDLETON",
			"second@example.com",
			now.AddDays(-4),
			now.AddDays(-3));
		var emailMatch = CreateOrder(
			"Third",
			"Candidate",
			"contains.needle@example.com",
			now.AddDays(-3),
			now.AddDays(-2));
		var nonMatch = CreateOrder(
			"Different",
			"Candidate",
			"different@example.com",
			now.AddDays(-2),
			now.AddDays(-1));
		var expiredMatch = CreateOrder(
			"Needle",
			"Expired",
			"expired.needle@example.com",
			now.AddDays(-40),
			now.AddDays(-31));

		await AddOrdersAsync(firstNameMatch, lastNameMatch, emailMatch, nonMatch, expiredMatch);
		var service = CreateService(CreateSuccessfulEmailService());

		// Act
		var result = await service.GetDisputeOrdersAsync(
			new PaginationRequest(PageIndex: 1, PageSize: 10, SearchTerm: "needle"),
			CancellationToken.None);

		// Assert
		result.Count.Should().Be(3);
		result.Data.Select(order => order.EmailInvitationID).Should().BeEquivalentTo([
			firstNameMatch.EmailInvitationID,
			lastNameMatch.EmailInvitationID,
			emailMatch.EmailInvitationID
		]);
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldSendEmailPersistDisputeAndInvalidateCachedList()
	{
		// Arrange
		var order = CreateOrder(
			"Ada",
			"Lovelace",
			"ada@example.com",
			DateTime.UtcNow.AddDays(-2),
			DateTime.UtcNow.AddDays(-1));
		await AddOrdersAsync(order);

		const string requestor = "requestor@example.com";
		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity([
				new Claim(ClaimTypes.Email, requestor)
			], "TestAuth"));

		var emailService = CreateSuccessfulEmailService();
		var service = CreateService(emailService);
		var pagination = new PaginationRequest(PageIndex: 1, PageSize: 10);
		var cachedBeforeUpdate = await service.GetDisputeOrdersAsync(
			pagination,
			CancellationToken.None);
		cachedBeforeUpdate.Data.Should().ContainSingle().Which.DisputeCategory.Should().BeNull();

		var request = new DisputeOrderRequestDTO
		{
			EmailInvitationId = order.EmailInvitationID,
			DisputeReason = "Report"
		};
		var startedAt = DateTime.UtcNow;

		// Act
		var result = await service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			CancellationToken.None);

		// Assert
		result.Should().BeTrue();
		_dbContext.ChangeTracker.Clear();

		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == order.EmailInvitationID);
		persisted.DisputeCategory.Should().Be("Report");
		persisted.DisputedAt.Should().NotBeNull();
		persisted.DisputedAt!.Value.Should().BeOnOrAfter(startedAt);
		persisted.DisputedAt.Value.Should().BeOnOrBefore(DateTime.UtcNow);

		var refreshed = await service.GetDisputeOrdersAsync(pagination, CancellationToken.None);
		refreshed.Data.Should().ContainSingle().Which.DisputeCategory.Should().Be("Report");

		var recipient = _configuration["ATS:DisputeOrderEmailRecipient"] ?? string.Empty;
		emailService.Verify(serviceMock => serviceMock.SendEmailForDispute(
			recipient,
			CompanyName,
			"Report",
			order.OrderCreatedAt,
			requestor,
			"Ada Lovelace"), Times.Once);
		emailService.Verify(serviceMock => serviceMock.SendATSEmailAsync(
			recipient,
			"CIBI | Dispute Order Notification",
			"dispute-email-body"), Times.Once);
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldThrowAndPreserveOrder_WhenEmailCannotBeSent()
	{
		// Arrange
		var order = CreateOrder(
			"Email",
			"Failure",
			"email.failure@example.com",
			DateTime.UtcNow.AddDays(-2),
			DateTime.UtcNow.AddDays(-1));
		await AddOrdersAsync(order);

		var emailService = new Mock<IEmailService>();
		emailService
			.Setup(service => service.SendEmailForDispute(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.Returns("dispute-email-body");
		emailService
			.Setup(service => service.SendATSEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync(false);

		var service = CreateService(emailService);
		var request = CreateDisputeRequest(order);

		// Act
		Func<Task> act = () => service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to send dispute order notification email.");

		_dbContext.ChangeTracker.Clear();
		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == order.EmailInvitationID);
		persisted.DisputeCategory.Should().BeNull();
		persisted.DisputedAt.Should().BeNull();
	}

	[Fact]
	public async Task MarkAsDisputedAsync_ShouldWrapRepositoryCancellationAndPreserveOrder()
	{
		// Arrange
		var order = CreateOrder(
			"Repository",
			"Failure",
			"repository.failure@example.com",
			DateTime.UtcNow.AddDays(-2),
			DateTime.UtcNow.AddDays(-1));
		await AddOrdersAsync(order);

		var emailService = CreateSuccessfulEmailService();
		var service = CreateService(emailService);
		var request = CreateDisputeRequest(order);
		using var cancellationSource = new CancellationTokenSource();
		cancellationSource.Cancel();

		// Act
		Func<Task> act = () => service.MarkAsDisputedAsync(
			request,
			AuthenticatedUserId,
			cancellationSource.Token);

		// Assert
		await act.Should()
			.ThrowAsync<InternalServerException>()
			.WithMessage("Failed to mark order as disputed.");

		_dbContext.ChangeTracker.Clear();
		var persisted = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == order.EmailInvitationID);
		persisted.DisputeCategory.Should().BeNull();
		persisted.DisputedAt.Should().BeNull();
		emailService.Verify(serviceMock => serviceMock.SendATSEmailAsync(
			It.IsAny<string>(),
			"CIBI | Dispute Order Notification",
			"dispute-email-body"), Times.Once);
	}

	#endregion

	private DisputeOrderService CreateService(Mock<IEmailService> emailService)
	{
		var userRepository = new Mock<IATSUserRepository>();
		userRepository
			.Setup(repository => repository.GetUserClientAssignmentsAsync(
				It.Is<IReadOnlyCollection<Guid>>(userIds =>
					userIds.Count == 1 && userIds.Contains(AuthenticatedUserId)),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([
				new UserClientDetailsDTO
				{
					UserId = AuthenticatedUserId,
					ClientId = 7,
					ClientName = CompanyName
				}
			]);

		return new DisputeOrderService(
			NullLogger<DisputeOrderService>.Instance,
			emailService.Object,
			_configuration,
			_atsRepository,
			userRepository.Object,
			_httpContextAccessor);
	}

	private static Mock<IEmailService> CreateSuccessfulEmailService()
	{
		var emailService = new Mock<IEmailService>();
		emailService
			.Setup(service => service.SendEmailForDispute(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.Returns("dispute-email-body");
		emailService
			.Setup(service => service.SendATSEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync(true);

		return emailService;
	}

	private async Task AddOrdersAsync(params EmailInvitationRequest[] orders)
	{
		await _dbContext.EmailInvitationRequests.AddRangeAsync(orders);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();
	}

	private static DisputeOrderRequestDTO CreateDisputeRequest(EmailInvitationRequest order) => new()
	{
		EmailInvitationId = order.EmailInvitationID,
		DisputeReason = "Billing"
	};

	private static EmailInvitationRequest CreateOrder(
		string firstName,
		string lastName,
		string email,
		DateTime orderCreatedAt,
		DateTime orderCompletedAt,
		string? disputeCategory = null,
		string orderStatus = "Completed")
	{
		var id = Guid.CreateVersion7();
		var now = DateTime.UtcNow;

		return new EmailInvitationRequest
		{
			EmailInvitationID = id,
			FirstName = firstName,
			LastName = lastName,
			MiddleInitial = firstName[..1],
			EmailAddress = email,
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
			OrderCreatedAt = orderCreatedAt,
			OrderCompletedAt = orderCompletedAt,
			DisputeCategory = disputeCategory
		};
	}
}
