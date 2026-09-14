using ATS.Configuration;
using ATS.Services.EmailAccounts;
using ATS.Services.EmailService;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The switcher loop itself - walking from one account to the next within a single message.
/// </summary>
/// <remarks>
/// The processor tests stub the send, so they prove what the pass does with an answer rather than
/// how the answer is reached. This covers the loop in
/// <c>ATSEmailService.SendATSEmailWithResultAsync</c>: that it excludes accounts it already tried,
/// that it is bounded by the number of registered accounts, and - the one with teeth - that a run
/// of account failures is translated to Throttled on the way out rather than reported verbatim.
///
/// The failures are injected through <c>GetContextAsync</c>, which is a real path (a deleted
/// account, or a password that no longer decrypts after a key rotation) and the only account-scoped
/// failure reachable without an SMTP server.
/// </remarks>
public class AtsEmailFailoverTests
{
	private const string UndecryptablePassword =
		"The stored password for account 1 could not be decrypted.";

	private static ATSEmailService Service(Mock<ISmtpAccountPoolRegistry> registry)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "ATS:ATSApplicationFormExpiryInHours", "72" }
			})
			.Build();

		return new ATSEmailService(
			configuration,
			NullLogger<ATSEmailService>.Instance,
			registry.Object,
			Options.Create(new AtsEmailDeliveryOptions()));
	}

	/// <summary>
	/// Hands out the given accounts in order, honouring the exclusion list the loop passes in.
	/// </summary>
	/// <remarks>
	/// Honouring the exclusions is what makes a runaway loop show up as a hanging test rather than
	/// a passing one: a registry that ignored them would return account 1 forever.
	/// </remarks>
	private static Mock<ISmtpAccountPoolRegistry> RegistryWith(params int[] accountIds)
	{
		var registry = new Mock<ISmtpAccountPoolRegistry>();

		registry
			.Setup(x => x.GetNextSendableAccountAsync(
				It.IsAny<IReadOnlyCollection<int>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyCollection<int> excluded, CancellationToken _) =>
				accountIds
					.Where(id => !excluded.Contains(id))
					.Select(id => AtsEmailAccountFixture.Account(id, priority: id))
					.FirstOrDefault());

		return registry;
	}

	/// <summary>Makes every account fail to prepare, so the loop keeps moving.</summary>
	private static void EveryContextFails(Mock<ISmtpAccountPoolRegistry> registry) =>
		registry
			.Setup(x => x.GetContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException(UndecryptablePassword));

	[Fact]
	public async Task SendATSEmailWithResultAsync_ShouldTryEveryAccountOnce()
	{
		// Arrange: three registered accounts, all of them broken in a way that is the account's
		// fault. The message is worth moving, so the loop should walk the whole list.
		var registry = RegistryWith(1, 2, 3);

		EveryContextFails(registry);

		// Act
		await Service(registry).SendATSEmailWithResultAsync(
			"candidate@example.com",
			"Subject",
			"Body",
			CancellationToken.None);

		// Assert: each account attempted exactly once. More than once would mean the exclusion
		// list is not being carried, which is an infinite retry rather than a failover.
		foreach (var accountId in new[] { 1, 2, 3 })
		{
			registry.Verify(
				x => x.GetContextAsync(accountId, It.IsAny<CancellationToken>()),
				Times.Once);
		}
	}

	[Fact]
	public async Task SendATSEmailWithResultAsync_ShouldExcludeAnAccountThatAlreadyRefused()
	{
		// Arrange
		var registry = RegistryWith(1, 2);

		EveryContextFails(registry);

		var exclusionsSeen = new List<int[]>();

		registry
			.Setup(x => x.GetNextSendableAccountAsync(
				It.IsAny<IReadOnlyCollection<int>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyCollection<int> excluded, CancellationToken _) =>
			{
				exclusionsSeen.Add([.. excluded]);

				return new[] { 1, 2 }
					.Where(id => !excluded.Contains(id))
					.Select(id => AtsEmailAccountFixture.Account(id, priority: id))
					.FirstOrDefault();
			});

		// Act
		await Service(registry).SendATSEmailWithResultAsync(
			"candidate@example.com",
			"Subject",
			"Body",
			CancellationToken.None);

		// Assert: the list grows by the account that just refused, each time round.
		exclusionsSeen.Should().HaveCount(3);
		exclusionsSeen[0].Should().BeEmpty();
		exclusionsSeen[1].Should().Equal(1);
		exclusionsSeen[2].Should().Equal(1, 2);
	}

	[Fact]
	public async Task SendATSEmailWithResultAsync_ShouldReportThrottled_WhenEveryAccountRefused()
	{
		// Arrange: THE trap. Three accounts whose passwords no longer decrypt each produce a
		// Permanent, and a loop that returned the last one verbatim would tell the processor the
		// RECIPIENT was rejected - retiring a perfectly valid candidate address over our own
		// misconfiguration. Throttled is the only outcome that means "defer without charging an
		// attempt", which is the correct reading whatever the accounts said.
		var registry = RegistryWith(1, 2, 3);

		EveryContextFails(registry);

		// Act
		var result = await Service(registry).SendATSEmailWithResultAsync(
			"candidate@example.com",
			"Subject",
			"Body",
			CancellationToken.None);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Throttled);

		// The reason still travels, because "raise the daily limit" and "the password is wrong"
		// need very different responses from whoever reads the log.
		result.Message.Should().Contain(UndecryptablePassword);
	}

	[Fact]
	public async Task SendATSEmailWithResultAsync_ShouldReportThrottled_WhenNothingIsSendable()
	{
		// Arrange: every account capped, cooling down, unverified or disabled - so the loop never
		// gets a first account and there is no provider response to quote.
		var registry = RegistryWith();

		// Act
		var result = await Service(registry).SendATSEmailWithResultAsync(
			"candidate@example.com",
			"Subject",
			"Body",
			CancellationToken.None);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Throttled);
		result.Message.Should().Contain("Every registered sender account");

		registry.Verify(
			x => x.GetContextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task SendATSEmailAsync_ShouldReportFailure_WhenTheAccountsAreExhausted()
	{
		// Arrange: the bool-returning overload the dispute mail and single enrolment still use.
		// It must not read "no account available" as success.
		var registry = RegistryWith();

		// Act
		var sent = await Service(registry).SendATSEmailAsync(
			"candidate@example.com",
			"Subject",
			"Body");

		// Assert
		sent.Should().BeFalse();
	}
}
