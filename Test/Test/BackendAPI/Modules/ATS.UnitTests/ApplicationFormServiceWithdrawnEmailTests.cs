using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.Data.UnitOfWork;
using ATS.DTO;
using ATS.Services.ApplicationForm;
using ATS.Services.EmailService;
using ATS.Services.FilePDFService;
using ATS.Services.Notifications;
using ATS.Services.OrderHistory;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The withdrawal notice's place in the withdrawal itself: it happens, it happens only after the
/// commit, and it does not happen for a withdrawal that never landed.
/// </summary>
/// <remarks>
/// The notice's own behaviour - who it addresses, who is copied, what it says, and that a delivery
/// failure is swallowed - is covered by <c>WithdrawnEmailNotificationTests</c> and
/// <c>AtsWithdrawnEmailBodyTests</c>. These tests stop at <see cref="IWithdrawnEmailNotification"/>,
/// because that is the seam this service owns.
/// </remarks>
public class ApplicationFormServiceWithdrawnEmailTests
{
	private const string HashToken = "withdrawn-hash-token";

	private static readonly Guid InvitationId = Guid.CreateVersion7();
	private static readonly Guid RequestorId = Guid.CreateVersion7();

	private readonly Mock<ILogger<ApplicationFormService>> _logger = new();
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IUnitOfWork> _unitOfWork = new();
	private readonly Mock<IObjectStorageService> _objectStorage = new();
	private readonly Mock<IFilePdfService> _filePdfService = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();
	private readonly Mock<IAtsNotificationService> _notificationService = new();
	private readonly Mock<IWithdrawnEmailNotification> _withdrawnEmail = new();

	private readonly ApplicationFormService _service;

	public ApplicationFormServiceWithdrawnEmailTests()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ATS:ApplicationFormBaseUrl"] = "https://forms.cibi.test/",
				["ATS:ATSApplicationFormFileFolderName"] = "ats-application-forms"
			})
			.Build();

		_service = new ApplicationFormService(
			_logger.Object,
			_repository.Object,
			_unitOfWork.Object,
			configuration,
			_objectStorage.Object,
			_filePdfService.Object,
			_orderHistoryService.Object,
			_notificationService.Object,
			_withdrawnEmail.Object);
	}

	/// <summary>
	/// Arranges a withdrawal that reaches the commit: the hash token resolves, the row updates, and
	/// the transaction is accepted. Returns the invitation the service loaded, so a test can assert
	/// it is the one handed to the notifier.
	/// </summary>
	private EmailInvitationRequest SetupCommittedWithdrawal()
	{
		var invitation = new EmailInvitationRequest
		{
			EmailInvitationID = InvitationId,
			RequestorId = RequestorId,
			Requestor = "Ana Reyes",
			EmailAddress = "candidate@example.test",
			FirstName = "Juan",
			LastName = "Dela Cruz"
		};

		_repository
			.Setup(repository => repository.GetEmailIdAndApplicationFormPathAsync(
				HashToken,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new EmailIdAndApplicationFormPathDTO { EmailId = InvitationId });
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				InvitationId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(invitation);
		_repository
			.Setup(repository => repository.WithdrawnApplicationForm(
				HashToken,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(1);

		return invitation;
	}

	[Fact]
	public async Task WithdrawnApplicationForm_ShouldNotifyWithTheLoadedOrder_WhenTheWithdrawalCommits()
	{
		// Arrange
		var invitation = SetupCommittedWithdrawal();

		// Act
		var result = await _service.WithdrawnApplicationForm(HashToken, CancellationToken.None);

		// Assert: the notifier receives the order the service already loaded, so the notice needs
		// no second read of a row that was just updated.
		result.Should().BeTrue();
		_withdrawnEmail.Verify(
			notifier => notifier.SendAsync(invitation, CancellationToken.None),
			Times.Once);
		_unitOfWork.Verify(uow => uow.CommitAsync(CancellationToken.None), Times.Once);
	}

	[Fact]
	public async Task WithdrawnApplicationForm_ShouldNotNotify_WhenTheCommitFails()
	{
		// Arrange
		SetupCommittedWithdrawal();
		_unitOfWork
			.Setup(uow => uow.CommitAsync(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("Commit failed."));

		// Act
		Func<Task> act = () => _service.WithdrawnApplicationForm(HashToken, CancellationToken.None);

		// Assert: the notice is strictly after the commit. Announcing a withdrawal that did not
		// land would tell a requestor to stop a verification that is still running.
		await act.Should().ThrowAsync<InvalidOperationException>();
		_withdrawnEmail.Verify(
			notifier => notifier.SendAsync(
				It.IsAny<EmailInvitationRequest>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
		_unitOfWork.Verify(uow => uow.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task WithdrawnApplicationForm_ShouldNotNotify_WhenTheTokenMatchesNoOrder()
	{
		// Arrange: the token resolves to an invitation id, but the update moves no rows - the form
		// was already submitted or already withdrawn.
		_repository
			.Setup(repository => repository.GetEmailIdAndApplicationFormPathAsync(
				HashToken,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new EmailIdAndApplicationFormPathDTO { EmailId = InvitationId });
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				InvitationId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new EmailInvitationRequest { EmailInvitationID = InvitationId });
		_repository
			.Setup(repository => repository.WithdrawnApplicationForm(
				HashToken,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(0);

		// Act
		Func<Task> act = () => _service.WithdrawnApplicationForm(HashToken, CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();
		_withdrawnEmail.Verify(
			notifier => notifier.SendAsync(
				It.IsAny<EmailInvitationRequest>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}
}
