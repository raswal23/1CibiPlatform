using ATS.Configuration;
using ATS.Constants;
using ATS.Services.EmailService;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// When a failure takes an account out of rotation, and - more importantly - when it must not.
/// </summary>
/// <remarks>
/// The rule that matters most here is the one that looks like a missed opportunity: a `550 no
/// such mailbox` is about the candidate's address, so it never counts against the account. It is
/// the rule most likely to be "fixed" later by someone who reads the breaker and sees a failure
/// going uncounted, and the cost of getting it wrong is one bulk upload of typo'd addresses
/// retiring every registered sender in minutes.
/// </remarks>
public class AtsEmailAccountBreakerTests
{
	private static AtsEmailAccountFixture Fixture(int threshold = 3) =>
		new(new AtsEmailDeliveryOptions
		{
			ConsecutiveFailureThreshold = threshold,
			TransientFailureCooldownSeconds = 900,
			ThrottleBackoffSeconds = 600
		});

	[Fact]
	public async Task ReportFailureAsync_ShouldNotCountARecipientRejection()
	{
		// Arrange: THE rule. A 550 is the server talking about the address we sent to, not about
		// the mailbox we sent from.
		var fixture = Fixture();

		fixture.HasAccounts(AtsEmailAccountFixture.Account(id: 1, priority: 1));

		// Act
		var tripped = await fixture.Registry.ReportFailureAsync(
			1,
			EmailDeliveryResult.Permanent("550", "550 5.1.1 No such user here"),
			CancellationToken.None);

		// Assert
		tripped.Should().BeFalse();

		// Not merely "did not trip" - it never reached the health row at all. A 550 that wrote a
		// LastFailureReason would make a healthy account look broken on the management board.
		fixture.Repository.Verify(
			x => x.RecordHealthAsync(
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task ReportFailureAsync_ShouldCoolDownOnTheFirstThrottle()
	{
		// Arrange: the provider has already said this account is sending too fast. A second
		// opinion costs another message against a closed door, so there is nothing to count.
		var fixture = Fixture();

		fixture.HasAccounts(AtsEmailAccountFixture.Account(id: 1, priority: 1));

		DateTime? cooledUntil = null;

		fixture.Repository
			.Setup(x => x.RecordHealthAsync(
				1,
				It.IsAny<int>(),
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.Callback<int, int, DateTime?, string?, string?, CancellationToken>(
				(_, _, until, _, _, _) => cooledUntil = until)
			.Returns(Task.CompletedTask);

		// Act
		var tripped = await fixture.Registry.ReportFailureAsync(
			1,
			EmailDeliveryResult.Throttled("454", "454 4.7.0 Too many login attempts"),
			CancellationToken.None);

		// Assert
		tripped.Should().BeTrue();
		cooledUntil.Should().NotBeNull();
		cooledUntil!.Value.Should().BeAfter(DateTime.UtcNow);

		// The counter is untouched, not incremented. A throttle is not evidence of three
		// transients - charging one would retire the account early once it recovers.
		fixture.Repository.Verify(
			x => x.RecordHealthAsync(
				1,
				0,
				It.IsAny<DateTime?>(),
				It.IsAny<string>(),
				null,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ReportFailureAsync_ShouldFlagForReverification_WhenTheProviderRejectsTheCredentials()
	{
		// Arrange: a revoked or rotated app password. It arrives as an account-scoped Permanent
		// with no status code at all, because MailKit's AuthenticationException carries none -
		// which is exactly why the scope travels on the result rather than being sniffed from
		// "535" downstream.
		var fixture = Fixture();

		fixture.HasAccounts(AtsEmailAccountFixture.Account(id: 1, priority: 1));

		// Act
		var tripped = await fixture.Registry.ReportFailureAsync(
			1,
			EmailDeliveryResult.Permanent(
				null,
				"Authentication failed. Check the app password.",
				EmailFailureScope.Account),
			CancellationToken.None);

		// Assert
		tripped.Should().BeTrue();

		// NeedsReverification rather than a cooldown: nothing about waiting fixes a password the
		// provider has revoked, so a timer would only readmit it to fail identically.
		fixture.Repository.Verify(
			x => x.RecordHealthAsync(
				1,
				0,
				null,
				It.IsAny<string>(),
				AtsEmailAccountStatus.NeedsReverification,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ReportFailureAsync_ShouldNotTrip_OnASingleTransient()
	{
		// Arrange: one dropped socket is noise. Retiring an account for it would leave a
		// two-account setup on one sender for a quarter of an hour over nothing.
		var fixture = Fixture(threshold: 3);

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(id: 1, priority: 1, consecutiveFailureCount: 0));

		// Act
		var tripped = await fixture.Registry.ReportFailureAsync(
			1,
			EmailDeliveryResult.Transient("421", "Connection reset", EmailFailureScope.Account),
			CancellationToken.None);

		// Assert
		tripped.Should().BeFalse();

		// Counted but not cooled: the count is what turns the third one into a pattern.
		fixture.Repository.Verify(
			x => x.RecordHealthAsync(
				1,
				1,
				null,
				It.IsAny<string>(),
				null,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ReportFailureAsync_ShouldTrip_OnTheThirdConsecutiveTransient()
	{
		// Arrange: two already recorded, so this one crosses the threshold.
		var fixture = Fixture(threshold: 3);

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(id: 1, priority: 1, consecutiveFailureCount: 2));

		// Act
		var tripped = await fixture.Registry.ReportFailureAsync(
			1,
			EmailDeliveryResult.Transient(null, "Timed out", EmailFailureScope.Account),
			CancellationToken.None);

		// Assert
		tripped.Should().BeTrue();

		// Reset to zero on the trip, not left at the threshold. Leaving it would re-trip the
		// account on its very first failure after the cooldown rather than giving it a fresh three.
		fixture.Repository.Verify(
			x => x.RecordHealthAsync(
				1,
				0,
				It.Is<DateTime?>(until => until != null && until > DateTime.UtcNow),
				It.IsAny<string>(),
				null,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ReportSuccessAsync_ShouldResetTheCounter_AndRecordConsumption()
	{
		// Arrange: any successful send clears the count, so three failures spread across a
		// working day never add up to a trip.
		var fixture = Fixture();

		fixture.HasAccounts(AtsEmailAccountFixture.Account(id: 1, priority: 1));

		// Act
		await fixture.Registry.ReportSuccessAsync(1, 1, CancellationToken.None);

		// Assert: one call, because the log row and the reset cannot be allowed to diverge - an
		// account whose consumption advanced but whose count did not reset would be retired while
		// demonstrably working.
		fixture.Repository.Verify(
			x => x.RecordSuccessfulSendAsync(
				1,
				1,
				It.IsAny<DateTime>(),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task IsLeased_ShouldBeTrueOnlyWhileASendIsInFlight()
	{
		// Arrange
		var fixture = Fixture();

		// Act & Assert
		fixture.Registry.IsLeased(1).Should().BeFalse();

		using (fixture.Registry.Lease(1))
		{
			fixture.Registry.IsLeased(1).Should().BeTrue();
		}

		fixture.Registry.IsLeased(1).Should().BeFalse();
	}

	[Fact]
	public void IsLeased_ShouldStayTrue_UntilTheLastConcurrentSendFinishes()
	{
		// Arrange: counted rather than a flag. An account sends several messages at once, and a
		// flag cleared by the first one to finish would let an edit land in the middle of the rest.
		var fixture = Fixture();

		var first = fixture.Registry.Lease(1);
		var second = fixture.Registry.Lease(1);

		// Act
		first.Dispose();

		// Assert
		fixture.Registry.IsLeased(1).Should().BeTrue();

		second.Dispose();

		fixture.Registry.IsLeased(1).Should().BeFalse();
	}
}
