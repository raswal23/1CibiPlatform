using ATS.Constants;
using ATS.Data.DataSeed;
using ATS.Shared;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The seeded contents of <c>ats."EmailProcessDetails"</c>.
/// </summary>
/// <remarks>
/// This suite exists to replace something the cutover removed. While the copy lists were
/// <c>const string[]</c> on <c>WithdrawnEmail</c>, <c>DisputeEmail</c> and friends, the notification
/// suites asserted against those constants, so changing who CIBI copies on a candidate notice broke
/// a test and someone had to look at it. Moving the lists into a table deliberately ended that: the
/// send-path suites now stub the resolver, because what they are testing is what a notice does with
/// a list, not which list. Nothing was left pinning the agreed addresses themselves.
///
/// This is that pin, moved to where the addresses now live. It is the only place in the module where
/// a literal CIBI mailbox is asserted, and changing one here should be a deliberate edit with a
/// reason, not a side effect of tidying the seed.
///
/// It matters more than the constants version did, for two reasons. The seed is the ONLY definition
/// of these addresses now - there is no compiled copy to fall back on - and it runs in production:
/// <c>AppConfiguration.UseEnvironmentAsync</c> seeds Development, Sandbox, UAT and Production alike.
/// A tester mailbox committed here reaches real candidate mail on the next deploy. That has already
/// nearly happened once: the constants spent a stretch swapped to a personal gmail address for
/// manual testing, and the note on <c>GetEmailProcesses</c> exists because copying that swap forward
/// into the seed would have shipped it.
///
/// What this suite does NOT assert is that production actually holds these values. The seeder only
/// adds rows for processes that have none, so an operator's edit survives every restart - which is
/// the point of the management screen. This pins the STARTING state of a new environment.
/// </remarks>
public class EmailProcessSeedTests
{
	private const string ClientSupport = "clientsupport@cibi.com.ph";
	private const string PreWorkTeam = "pre-workteam@cibi.com.ph";

	private static string CopyListFor(string emailProcess) =>
		ATSInitialData.GetEmailProcesses()
			.Single(process => process.EmailProcess == emailProcess)
			.CCEmail;

	/// <summary>
	/// One row per process, no extras. A value in the constant with no row would be a notice whose
	/// copy list the management screen cannot show and the resolver cannot find.
	/// </summary>
	[Fact]
	public void GetEmailProcesses_ShouldSeedExactlyOneRowPerKnownProcess()
	{
		var emailProcesses = ATSInitialData.GetEmailProcesses();

		emailProcesses
			.Select(process => process.EmailProcess)
			.Should()
			.BeEquivalentTo(AtsEmailProcess.All);
	}

	/// <summary>
	/// The addresses the withdrawn and dispute notices were agreed to copy. Both went to client
	/// support alone before the cutover and still do.
	/// </summary>
	[Theory]
	[InlineData(AtsEmailProcess.Withdrawn)]
	[InlineData(AtsEmailProcess.Dispute)]
	public void GetEmailProcesses_ShouldCopyClientSupportOnly_ForTheOrderNotices(string emailProcess)
	{
		EmailCopyList.Split(CopyListFor(emailProcess))
			.Should()
			.Equal(ClientSupport);
	}

	/// <summary>
	/// The three candidate-facing notices copy both teams. The invitation and the reminder are
	/// separate rows that happen to hold the same addresses - seeding them identically is what made
	/// the cutover a no-op, and an operator can now diverge them without a deploy.
	/// </summary>
	[Theory]
	[InlineData(AtsEmailProcess.ApplicationForm)]
	[InlineData(AtsEmailProcess.FollowUp)]
	[InlineData(AtsEmailProcess.SubmittedForm)]
	public void GetEmailProcesses_ShouldCopyBothTeams_ForTheCandidateNotices(string emailProcess)
	{
		EmailCopyList.Split(CopyListFor(emailProcess))
			.Should()
			.Equal(ClientSupport, PreWorkTeam);
	}

	/// <summary>
	/// Every seeded address is a CIBI mailbox.
	/// </summary>
	/// <remarks>
	/// Broader than the per-process assertions above and aimed at a different mistake. Those say
	/// "this row holds exactly these addresses" and catch a deliberate change; this one catches the
	/// accident - a tester mailbox left in the seed after a manual run. It fails on any domain but
	/// cibi.com.ph, so a gmail address added to any process, present or future, fails here even if
	/// nobody updated the assertions above to cover it.
	/// </remarks>
	[Fact]
	public void GetEmailProcesses_ShouldSeedOnlyCibiMailboxes()
	{
		var addresses = ATSInitialData.GetEmailProcesses()
			.SelectMany(process => EmailCopyList.Split(process.CCEmail));

		addresses.Should().OnlyContain(address => address.EndsWith("@cibi.com.ph"));
	}

	/// <summary>
	/// Every seeded list is one the send path can hand to MimeKit: no malformed entry, no duplicate,
	/// nothing over the column's limit. Asserted through the same validator the management screen
	/// uses, so the seed cannot ship a row an operator would be forbidden from typing.
	/// </summary>
	[Fact]
	public void GetEmailProcesses_ShouldSeedListsThatPassTheCopyListRules()
	{
		foreach (var emailProcess in ATSInitialData.GetEmailProcesses())
		{
			EmailCopyList.Validate(emailProcess.CCEmail)
				.Should()
				.BeNull($"the seeded copy list for {emailProcess.EmailProcess} has to be storable");
		}
	}

	/// <summary>
	/// Active only where there is somebody to copy. An active row with an empty list is what hands
	/// "" to <c>MailboxAddress.Parse</c> on the first send of that notice, and it throws for the
	/// whole message - so the flag and the list have to agree from the very first boot.
	/// </summary>
	[Fact]
	public void GetEmailProcesses_ShouldActivateExactlyTheRowsThatHaveAddresses()
	{
		foreach (var emailProcess in ATSInitialData.GetEmailProcesses())
		{
			emailProcess.IsActive
				.Should()
				.Be(
					EmailCopyList.Split(emailProcess.CCEmail).Count > 0,
					$"{emailProcess.EmailProcess} is active only if it copies somebody");
		}
	}
}
