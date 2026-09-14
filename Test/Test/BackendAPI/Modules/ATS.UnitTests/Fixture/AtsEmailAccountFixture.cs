using ATS.Configuration;
using ATS.Constants;
using ATS.Data.Repository.EmailAccounts;
using ATS.Services.EmailAccounts;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

/// <summary>
/// Builds a real <see cref="SmtpAccountPoolRegistry"/> over a mocked repository.
/// </summary>
/// <remarks>
/// The registry is the object under test in the selector and breaker suites, not a collaborator,
/// so it is constructed for real. Only the database behind it is faked.
///
/// It is a singleton that resolves its repository from <c>IServiceScopeFactory</c> per call - it
/// cannot inject a scoped DbContext - so the scope factory has to be wired up here for any of its
/// methods to reach the repository at all.
/// </remarks>
public sealed class AtsEmailAccountFixture
{
	public Mock<IAtsEmailAccountRepository> Repository { get; } = new();

	public AtsEmailDeliveryOptions Options { get; }

	public SmtpAccountPoolRegistry Registry { get; }

	public AtsEmailAccountFixture(AtsEmailDeliveryOptions? options = null)
	{
		Options = options ?? new AtsEmailDeliveryOptions
		{
			ConsecutiveFailureThreshold = 3,
			TransientFailureCooldownSeconds = 900,
			ThrottleBackoffSeconds = 600
		};

		var scope = new Mock<IServiceScope>();
		var provider = new Mock<IServiceProvider>();

		provider
			.Setup(x => x.GetService(typeof(IAtsEmailAccountRepository)))
			.Returns(Repository.Object);

		scope.Setup(x => x.ServiceProvider).Returns(provider.Object);

		var scopeFactory = new Mock<IServiceScopeFactory>();

		scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

		Registry = new SmtpAccountPoolRegistry(
			scopeFactory.Object,
			new Mock<ISecretProtector>().Object,
			Microsoft.Extensions.Options.Options.Create(Options),
			NullLoggerFactory.Instance,
			NullLogger<SmtpAccountPoolRegistry>.Instance);
	}

	/// <summary>A verified, active, uncapped account at the given priority.</summary>
	public static AtsEmailAccountSnapshot Account(
		int id,
		int priority,
		bool isActive = true,
		string status = AtsEmailAccountStatus.Verified,
		DateTime? coolingDownUntil = null,
		int dailySendLimit = 450,
		int consumedInWindow = 0,
		int consecutiveFailureCount = 0) =>
		new(
			AtsEmailAccountId: id,
			DisplayName: $"Sender {id}",
			EmailAddress: $"sender{id}@example.com",
			SmtpHost: "smtp.example.com",
			SmtpPort: 587,
			Priority: priority,
			IsActive: isActive,
			DailySendLimit: dailySendLimit,
			VerificationStatus: status,
			VerifiedAt: status == AtsEmailAccountStatus.Verified ? DateTime.UtcNow.AddDays(-1) : null,
			ConsecutiveFailureCount: consecutiveFailureCount,
			CoolingDownUntil: coolingDownUntil,
			LastFailureReason: null,
			LastSentAt: null,
			ConsumedInWindow: consumedInWindow);

	/// <summary>Makes <c>GetSnapshotsAsync</c> answer with these accounts, priority order first.</summary>
	public void HasAccounts(params AtsEmailAccountSnapshot[] accounts)
	{
		var ordered = accounts.OrderBy(account => account.Priority).ToList();

		Repository
			.Setup(x => x.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(ordered);

		foreach (var account in ordered)
		{
			Repository
				.Setup(x => x.GetSnapshotAsync(account.AtsEmailAccountId, It.IsAny<CancellationToken>()))
				.ReturnsAsync(account);
		}
	}
}
