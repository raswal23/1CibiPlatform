using ATS.Constants;
using ATS.Data.Repository;
using ATS.DTO;
using ATS.Services.Settings.EmailProcessManagement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// <c>EmailProcessManagementService.GetCopyListAsync</c> - the read that turns a row in
/// <c>ats."EmailProcessDetails"</c> into the Cc on a notice.
/// </summary>
/// <remarks>
/// Separate from <c>EmailProcessManagementServiceIntegrationTests</c>, which covers the same
/// service's write side, because the two halves answer to opposite rules. The writes throw at an
/// operator who can see the screen; this read must never throw at a send path that has nobody
/// watching. Testing them together would blur which stance belongs to which method.
///
/// Worth the coverage because almost none of it is the happy path. The copy lists were once
/// <c>const string[]</c>, which cannot be missing, cannot be switched off and cannot fail to load;
/// moving them into a table bought an operator the ability to edit them and bought the send path
/// four new ways to get nothing back. Every one of those has to degrade to an empty list, because
/// the notice above it is already committed work - a candidate has submitted their form, an order
/// has been withdrawn - and losing the copy is a smaller failure than losing the message.
///
/// The logging is deliberately NOT asserted. Nothing else in this module verifies a log call, and
/// pinning message text here would fail on a reword that changed no behaviour. What matters to a
/// caller is the empty list; the warning is for whoever reads the logs afterwards. The levels are
/// still chosen carefully - warning for a missing row, information for one switched off - and that
/// distinction is recorded on the method itself.
/// </remarks>
public class EmailProcessCopyListTests
{
	private const string ClientSupport = "clientsupport@cibi.com.ph";
	private const string PreWorkTeam = "pre-workteam@cibi.com.ph";

	private readonly Mock<IEmailProcessRepository> _emailProcessRepository = new();

	private readonly EmailProcessManagementService _service;

	public EmailProcessCopyListTests()
	{
		_service = new EmailProcessManagementService(
			_emailProcessRepository.Object,
			Mock.Of<ILogger<EmailProcessManagementService>>());
	}

	private static EmailProcessDetailsDTO Row(
		string emailProcess,
		string ccEmail,
		bool isActive = true) =>
		new()
		{
			Id = 1,
			EmailProcess = emailProcess,
			CCEmail = ccEmail,
			CreatedDate = DateTime.UtcNow,
			IsActive = isActive
		};

	private void HaveRows(params EmailProcessDetailsDTO[] rows) =>
		_emailProcessRepository
			.Setup(repository => repository.GetEmailProcessesAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([.. rows]);

	[Fact]
	public async Task GetCopyListAsync_ShouldReturnTheAddressesOnTheMatchingRow()
	{
		HaveRows(
			Row(AtsEmailProcess.Withdrawn, ClientSupport),
			Row(AtsEmailProcess.Dispute, $"{ClientSupport},{PreWorkTeam}"));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Dispute,
			CancellationToken.None);

		copyList.Should().Equal(ClientSupport, PreWorkTeam);
	}

	/// <summary>
	/// The reason the two rows exist as separate processes: reading one must not answer with the
	/// other's list, even though the seed gives them the same addresses today.
	/// </summary>
	[Fact]
	public async Task GetCopyListAsync_ShouldNotReturnAnotherProcessesList()
	{
		HaveRows(
			Row(AtsEmailProcess.ApplicationForm, ClientSupport),
			Row(AtsEmailProcess.FollowUp, PreWorkTeam));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.FollowUp,
			CancellationToken.None);

		copyList.Should().Equal(PreWorkTeam);
	}

	/// <summary>
	/// The unique index is case-sensitive, so a row hand-inserted as "withdrawn" is a row
	/// PostgreSQL will happily keep and the send path would otherwise walk straight past. Matching
	/// loosely means such a row is used rather than silently ignored.
	/// </summary>
	[Theory]
	[InlineData("withdrawn")]
	[InlineData("WITHDRAWN")]
	[InlineData("WithDrawn")]
	public async Task GetCopyListAsync_ShouldMatchTheProcessCaseInsensitively(string storedProcess)
	{
		HaveRows(Row(storedProcess, ClientSupport));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Withdrawn,
			CancellationToken.None);

		copyList.Should().Equal(ClientSupport);
	}

	/// <summary>
	/// Every value in <see cref="AtsEmailProcess.All"/> is seeded, so a missing row means the seed
	/// did not run or somebody deleted it. That is a fault worth a warning - and still not worth
	/// the notice.
	/// </summary>
	[Fact]
	public async Task GetCopyListAsync_ShouldReturnEmpty_WhenNoRowExistsForTheProcess()
	{
		HaveRows(Row(AtsEmailProcess.Dispute, ClientSupport));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Withdrawn,
			CancellationToken.None);

		copyList.Should().BeEmpty();
	}

	/// <summary>
	/// An inactive row is an operator saying "stop copying anyone on this notice". Honouring it is
	/// the whole point of the flag - reading the addresses anyway would make it decorative.
	/// </summary>
	[Fact]
	public async Task GetCopyListAsync_ShouldReturnEmpty_WhenTheRowIsSwitchedOff()
	{
		HaveRows(Row(AtsEmailProcess.Withdrawn, ClientSupport, isActive: false));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Withdrawn,
			CancellationToken.None);

		copyList.Should().BeEmpty();
	}

	[Fact]
	public async Task GetCopyListAsync_ShouldReturnEmpty_WhenTheRowHasNoAddresses()
	{
		HaveRows(Row(AtsEmailProcess.Withdrawn, string.Empty));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Withdrawn,
			CancellationToken.None);

		copyList.Should().BeEmpty();
	}

	/// <summary>
	/// The database being unreachable must not take the notice with it. This is the case the
	/// constants could not have - a compiled array cannot fail to load - and the reason the read
	/// goes through <c>SideEffectGuard</c> rather than being awaited directly.
	/// </summary>
	[Fact]
	public async Task GetCopyListAsync_ShouldReturnEmpty_WhenTheReadThrows()
	{
		_emailProcessRepository
			.Setup(repository => repository.GetEmailProcessesAsync(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("the database is not answering"));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Withdrawn,
			CancellationToken.None);

		copyList.Should().BeEmpty();
	}

	/// <summary>
	/// The column is hand-editable and a row written before <c>EmailCopyList</c> existed can carry
	/// "a@x.com, b@x.com". The leading space makes the second fragment unparseable to MimeKit,
	/// which throws for the WHOLE notice rather than for the one bad address - so the trim has to
	/// happen here, on the way out of the table.
	/// </summary>
	[Fact]
	public async Task GetCopyListAsync_ShouldTrimAndDropBlanks_OnAHandEditedRow()
	{
		HaveRows(Row(AtsEmailProcess.Withdrawn, $" {ClientSupport} , ,{PreWorkTeam}, "));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Withdrawn,
			CancellationToken.None);

		copyList.Should().Equal(ClientSupport, PreWorkTeam);
	}

	/// <summary>
	/// A malformed address is passed through rather than filtered out, which looks like the wrong
	/// choice until you consider the alternative: dropping it would leave the team quietly
	/// uncopied for as long as the typo survives, and nothing would ever report it. Letting the
	/// send fail makes the broken row visible.
	/// </summary>
	[Fact]
	public async Task GetCopyListAsync_ShouldNotFilterAMalformedAddress()
	{
		HaveRows(Row(AtsEmailProcess.Withdrawn, $"{ClientSupport},not-an-address"));

		var copyList = await _service.GetCopyListAsync(
			AtsEmailProcess.Withdrawn,
			CancellationToken.None);

		copyList.Should().Equal(ClientSupport, "not-an-address");
	}

	/// <summary>
	/// One read per resolve, answered by the cache decorator on the repository. Reading the whole
	/// table rather than one row is what lets that single cached entry serve every notice; a
	/// by-process query would miss the cache on each send.
	/// </summary>
	[Fact]
	public async Task GetCopyListAsync_ShouldReadTheWholeTableOnce()
	{
		HaveRows(Row(AtsEmailProcess.Withdrawn, ClientSupport));

		await _service.GetCopyListAsync(AtsEmailProcess.Withdrawn, CancellationToken.None);

		_emailProcessRepository.Verify(
			repository => repository.GetEmailProcessesAsync(It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
