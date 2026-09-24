using EmploymentVerification.Data.Entities;
using FluentAssertions;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// Pins which statuses block a segment from being requested again.
/// </summary>
/// <remarks>
/// The rule lives in <c>EmploymentVerificationRepository.ListBlockedSegmentsAsync</c> as
/// an EF predicate. It cannot be invoked directly here - the DbContext is sealed, so it
/// cannot be mocked, and the test project has no in-process EF provider - so this mirrors
/// the predicate and documents the intent behind each status. The query itself is
/// exercised against real PostgreSQL by the integration suite.
/// <para>
/// If the repository predicate changes, this must change with it. That duplication is
/// the price of keeping a fast test on a rule whose failure mode - re-mailing an employer
/// who declined - is not something to discover in production.
/// </para>
/// </remarks>
public class BlockedSegmentPredicateTests
{
	/// <summary>Mirror of the repository's status predicate.</summary>
	private static bool Blocks(
		VerificationRequestStatus status,
		bool tokenStillLive) =>
		status == VerificationRequestStatus.Pending
		|| status == VerificationRequestStatus.Verified
		|| status == VerificationRequestStatus.Rejected
		|| (status == VerificationRequestStatus.Sent && tokenStillLive);

	[Fact]
	public void Rejected_ShouldBlockPermanently_SoADeclinedEmployerIsNotMailedAgain()
	{
		// The behaviour this suite exists for. Rejected used to release the segment,
		// which was defensible while a human chose whether to retry; with a job sending
		// every five minutes it re-mails an employer who has just said "not accurate".
		Blocks(VerificationRequestStatus.Rejected, tokenStillLive: false).Should().BeTrue();
		Blocks(VerificationRequestStatus.Rejected, tokenStillLive: true).Should().BeTrue();
	}

	[Fact]
	public void Verified_ShouldBlockPermanently()
	{
		Blocks(VerificationRequestStatus.Verified, tokenStillLive: false).Should().BeTrue();
	}

	[Fact]
	public void Pending_ShouldBlock_RegardlessOfTheToken()
	{
		// Pending means the row was committed but the send has not completed. Blocking
		// prevents a second attempt racing the first.
		Blocks(VerificationRequestStatus.Pending, tokenStillLive: false).Should().BeTrue();
	}

	[Fact]
	public void Sent_ShouldBlock_OnlyWhileTheLinkIsStillUsable()
	{
		Blocks(VerificationRequestStatus.Sent, tokenStillLive: true).Should().BeTrue();

		// An unanswered link that lapsed is the one case worth retrying: nobody
		// declined, the employer simply never acted on it.
		Blocks(VerificationRequestStatus.Sent, tokenStillLive: false).Should().BeFalse();
	}

	[Fact]
	public void Expired_ShouldRelease_SoAFailedSendIsRetried()
	{
		// Expired is what a failed send is marked with. The row is already committed, so
		// it must be released or the segment is stranded - and it must not be marked
		// Rejected, which would read as a decline and block for good.
		Blocks(VerificationRequestStatus.Expired, tokenStillLive: false).Should().BeFalse();
		Blocks(VerificationRequestStatus.Expired, tokenStillLive: true).Should().BeFalse();
	}

	[Fact]
	public void EveryStatus_ShouldHaveADeliberateDecision()
	{
		// Guards the enum against a new member being added without anyone deciding
		// whether it blocks. A new value defaults to "releases", which is the unsafe
		// direction for anything meaning "already answered".
		var statuses = Enum.GetValues<VerificationRequestStatus>();

		statuses.Should().BeEquivalentTo(new[]
		{
			VerificationRequestStatus.Pending,
			VerificationRequestStatus.Sent,
			VerificationRequestStatus.Verified,
			VerificationRequestStatus.Rejected,
			VerificationRequestStatus.Expired
		});
	}
}
