using ATS.Constants;
using FluentAssertions;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// Which account carries the next message.
/// </summary>
/// <remarks>
/// The rule is "highest priority that is active, verified, past its cooldown and under its
/// rolling-24h cap". Each of those four is tested on its own, because a selector that quietly
/// drops one of them does not fail loudly - it sends through an account the provider has already
/// capped, and the queue stops with every row looking perfectly healthy.
/// </remarks>
public class AtsEmailAccountSelectorTests
{
	private static readonly IReadOnlyCollection<int> NoneExcluded = [];

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldPreferTheLowestPriorityNumber()
	{
		// Arrange: registered out of order, to prove the answer comes from Priority rather than
		// from the order the rows happened to arrive in.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(id: 3, priority: 3),
			AtsEmailAccountFixture.Account(id: 1, priority: 1),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account.Should().NotBeNull();
		account!.AtsEmailAccountId.Should().Be(1);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldSkipAnAccountThatIsCoolingDown()
	{
		// Arrange: the breaker tripped on the preferred account a minute ago.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(
				id: 1,
				priority: 1,
				coolingDownUntil: DateTime.UtcNow.AddMinutes(10)),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(2);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldReadmitAnAccountWhoseCooldownHasLapsed()
	{
		// Arrange: the cooldown is in the past, so the account comes back on its own. Nothing
		// clears CoolingDownUntil - the comparison is against now - so a stale timestamp must not
		// keep an account retired forever.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(
				id: 1,
				priority: 1,
				coolingDownUntil: DateTime.UtcNow.AddMinutes(-1)),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(1);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldSkipAnUnverifiedAccount()
	{
		// Arrange: a registration whose code was never confirmed. This is the guarantee that a
		// half-finished registration cannot reach the queue with an unproven password.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(
				id: 1,
				priority: 1,
				status: AtsEmailAccountStatus.Pending),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(2);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldSkipAnAccountNeedingReverification()
	{
		// Arrange: the provider rejected its app password. Waiting fixes nothing, so it stays out
		// until somebody re-enters the password.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(
				id: 1,
				priority: 1,
				status: AtsEmailAccountStatus.NeedsReverification),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(2);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldSkipAManuallyDisabledAccount()
	{
		// Arrange
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(id: 1, priority: 1, isActive: false),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(2);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldSkipAnAccountAtItsDailyCap()
	{
		// Arrange: the point of counting consumption at all. Moving BEFORE the provider refuses
		// costs one message; moving after costs roughly 24 hours of that mailbox.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(
				id: 1,
				priority: 1,
				dailySendLimit: 450,
				consumedInWindow: 450),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(2);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldStillUseAnAccountWithOneSendLeft()
	{
		// Arrange: the boundary. "Remaining > 0" rather than ">= some margin" - an account with a
		// single send left is healthy, and retiring it early throws away paid-for capacity.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(
				id: 1,
				priority: 1,
				dailySendLimit: 450,
				consumedInWindow: 449));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(1);
		account.RemainingInWindow.Should().Be(1);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldSkipAccountsAlreadyTriedForThisMessage()
	{
		// Arrange: failover. The message just bounced off account 1, and handing it straight back
		// would be an infinite loop rather than a retry.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(id: 1, priority: 1),
			AtsEmailAccountFixture.Account(id: 2, priority: 2));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			[1],
			CancellationToken.None);

		// Assert
		account!.AtsEmailAccountId.Should().Be(2);
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldReturnNull_WhenEveryAccountIsUnavailable()
	{
		// Arrange: one of each failure mode at once, which is what an exhausted registry actually
		// looks like. The processor reads null as "defer the rows without charging an attempt".
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts(
			AtsEmailAccountFixture.Account(id: 1, priority: 1, isActive: false),
			AtsEmailAccountFixture.Account(
				id: 2,
				priority: 2,
				status: AtsEmailAccountStatus.Pending),
			AtsEmailAccountFixture.Account(
				id: 3,
				priority: 3,
				coolingDownUntil: DateTime.UtcNow.AddMinutes(5)),
			AtsEmailAccountFixture.Account(
				id: 4,
				priority: 4,
				dailySendLimit: 10,
				consumedInWindow: 10));

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account.Should().BeNull();
	}

	[Fact]
	public async Task GetNextSendableAccountAsync_ShouldReturnNull_WhenNothingIsRegistered()
	{
		// Arrange: a fresh install before anybody registers a mailbox.
		var fixture = new AtsEmailAccountFixture();

		fixture.HasAccounts();

		// Act
		var account = await fixture.Registry.GetNextSendableAccountAsync(
			NoneExcluded,
			CancellationToken.None);

		// Assert
		account.Should().BeNull();
	}
}
