using ATS.Constants;
using ATS.Data.DataSeed;
using ATS.DTO;
using ATS.Shared;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class EmailProcessManagementServiceIntegrationTests : BaseIntegrationTest
{
	public EmailProcessManagementServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	private static AddEmailProcessDTO NewCopyList(
		string emailProcess = AtsEmailProcess.Withdrawn,
		string ccEmail = "clientsupport@cibi.com.ph",
		bool isActive = true) =>
		new()
		{
			EmailProcess = emailProcess,
			CCEmail = ccEmail,
			IsActive = isActive
		};

	#region Add

	[Fact]
	public async Task AddEmailProcessAsync_ShouldPersistTheCopyList_WhenTheProcessHasNoneYet()
	{
		// Arrange
		var copyList = NewCopyList(
			AtsEmailProcess.ApplicationForm,
			"clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph");

		// Act
		var result = await _emailProcessManagementService.AddEmailProcessAsync(
			copyList,
			CancellationToken.None);

		// Assert
		result.Id.Should().BeGreaterThan(0);
		result.EmailProcess.Should().Be(AtsEmailProcess.ApplicationForm);
		result.CCEmail.Should().Be("clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph");
		result.IsActive.Should().BeTrue();

		var persisted = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.SingleAsync(row => row.EmailProcess == AtsEmailProcess.ApplicationForm);

		persisted.CCEmail.Should().Be("clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph");
		persisted.IsActive.Should().BeTrue();

		// Stamped by the server, never by the caller - the DTO carries no CreatedDate.
		persisted.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
	}

	// The spacing an operator types is dropped on the way in, so the send path can split on a
	// bare comma without every reader having to trim.
	[Fact]
	public async Task AddEmailProcessAsync_ShouldNormalizeTheCopyList_WhenItCarriesSpacingAndBlanks()
	{
		// Arrange
		var copyList = NewCopyList(
			AtsEmailProcess.Dispute,
			"  clientsupport@cibi.com.ph ,, pre-workteam@cibi.com.ph  ");

		// Act
		var result = await _emailProcessManagementService.AddEmailProcessAsync(
			copyList,
			CancellationToken.None);

		// Assert
		result.CCEmail.Should().Be("clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph");

		var persisted = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.SingleAsync(row => row.EmailProcess == AtsEmailProcess.Dispute);

		persisted.CCEmail.Should().Be("clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph");
	}

	// The process name is also trimmed, so " Withdrawn " and "Withdrawn" cannot become two rows
	// for one notice.
	[Fact]
	public async Task AddEmailProcessAsync_ShouldTrimTheProcessName()
	{
		// Act
		var result = await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList($"  {AtsEmailProcess.FollowUp}  "),
			CancellationToken.None);

		// Assert
		result.EmailProcess.Should().Be(AtsEmailProcess.FollowUp);

		var persisted = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.AnyAsync(row => row.EmailProcess == AtsEmailProcess.FollowUp);

		persisted.Should().BeTrue();
	}

	// The unique index would reject this too, but as a DbUpdateException that reaches the
	// caller as a 500. The service turns it into the 400 the screen can render.
	[Fact]
	public async Task AddEmailProcessAsync_ShouldThrowBadRequest_WhenTheProcessAlreadyHasACopyList()
	{
		// Arrange
		await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.Withdrawn),
			CancellationToken.None);

		// Act
		var act = async () => await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.Withdrawn, "someoneelse@cibi.com.ph"),
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<BadRequestException>()
			.WithMessage($"*{AtsEmailProcess.Withdrawn}*already exists*");

		var rows = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.CountAsync(row => row.EmailProcess == AtsEmailProcess.Withdrawn);

		rows.Should().Be(1);
	}

	// Case-insensitive, unlike the index: "withdrawn" and "Withdrawn" are two rows to
	// PostgreSQL and one notice to the send path, so the index alone would let the pair
	// through and nothing would say which list wins.
	[Fact]
	public async Task AddEmailProcessAsync_ShouldThrowBadRequest_WhenOnlyTheCaseDiffers()
	{
		// Arrange
		await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.Withdrawn),
			CancellationToken.None);

		// Act
		var act = async () => await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList("withdrawn"),
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<BadRequestException>();
	}

	[Fact]
	public async Task AddEmailProcessAsync_ShouldPersistAnEmptyInactiveList()
	{
		// Act
		var result = await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.Withdrawn, ccEmail: string.Empty, isActive: false),
			CancellationToken.None);

		// Assert
		result.CCEmail.Should().BeEmpty();
		result.IsActive.Should().BeFalse();

		var persisted = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.SingleAsync(row => row.EmailProcess == AtsEmailProcess.Withdrawn);

		// The empty STRING, never null - the column is NOT NULL so a reader never has to treat
		// the two as the same thing.
		persisted.CCEmail.Should().NotBeNull().And.BeEmpty();
	}

	#endregion

	#region Edit

	[Fact]
	public async Task EditEmailProcessAsync_ShouldReplaceTheCopyListWholesale()
	{
		// Arrange
		var added = await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(
				AtsEmailProcess.ApplicationForm,
				"clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph"),
			CancellationToken.None);

		// Act
		var result = await _emailProcessManagementService.EditEmailProcessAsync(
			new EditEmailProcessDTO
			{
				Id = added.Id,
				CCEmail = " newteam@cibi.com.ph ",
				IsActive = true
			},
			CancellationToken.None);

		// Assert - the whole list is replaced, not merged: the screen edits it as one value, so
		// a merge would make a deliberate removal impossible.
		result.CCEmail.Should().Be("newteam@cibi.com.ph");

		var persisted = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.SingleAsync(row => row.Id == added.Id);

		persisted.CCEmail.Should().Be("newteam@cibi.com.ph");
	}

	// The process a row serves, and the date it was created, are the row's - not the caller's.
	// EditEmailProcessDTO carries neither, and this proves the edit leaves both alone.
	[Fact]
	public async Task EditEmailProcessAsync_ShouldLeaveTheProcessAndCreatedDateUntouched()
	{
		// Arrange
		var added = await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.Dispute),
			CancellationToken.None);

		var originalCreatedDate = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.Where(row => row.Id == added.Id)
			.Select(row => row.CreatedDate)
			.SingleAsync();

		// Act
		await _emailProcessManagementService.EditEmailProcessAsync(
			new EditEmailProcessDTO
			{
				Id = added.Id,
				CCEmail = "another@cibi.com.ph",
				IsActive = true
			},
			CancellationToken.None);

		// Assert
		var persisted = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.SingleAsync(row => row.Id == added.Id);

		persisted.EmailProcess.Should().Be(AtsEmailProcess.Dispute);
		persisted.CreatedDate.Should().BeCloseTo(originalCreatedDate, TimeSpan.FromMilliseconds(1));
	}

	// Deactivating keeps the addresses on file, which is the whole reason the row is soft
	// -flagged rather than deleted: turning it back on is one toggle, not a re-entry.
	[Fact]
	public async Task EditEmailProcessAsync_ShouldKeepTheAddresses_WhenTheListIsDeactivated()
	{
		// Arrange
		var added = await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.FollowUp, "clientsupport@cibi.com.ph"),
			CancellationToken.None);

		// Act
		var result = await _emailProcessManagementService.EditEmailProcessAsync(
			new EditEmailProcessDTO
			{
				Id = added.Id,
				CCEmail = "clientsupport@cibi.com.ph",
				IsActive = false
			},
			CancellationToken.None);

		// Assert
		result.IsActive.Should().BeFalse();
		result.CCEmail.Should().Be("clientsupport@cibi.com.ph");
	}

	[Fact]
	public async Task EditEmailProcessAsync_ShouldThrowNotFound_WhenTheRowDoesNotExist()
	{
		// Act
		var act = async () => await _emailProcessManagementService.EditEmailProcessAsync(
			new EditEmailProcessDTO
			{
				Id = 987654,
				CCEmail = "clientsupport@cibi.com.ph",
				IsActive = true
			},
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>()
			.WithMessage("*987654*");
	}

	#endregion

	#region Read-back

	// The read goes through the cache decorator, so this also proves the write invalidated the
	// emailprocess tag - without that, the second read would serve the pre-edit list.
	[Fact]
	public async Task GetEmailProcessesAsync_ShouldReflectAWriteImmediately()
	{
		// Arrange
		var added = await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.Withdrawn, "clientsupport@cibi.com.ph"),
			CancellationToken.None);

		var before = await _emailProcessManagementService.GetEmailProcessesAsync(CancellationToken.None);
		before.Should().ContainSingle(row => row.CCEmail == "clientsupport@cibi.com.ph");

		// Act
		await _emailProcessManagementService.EditEmailProcessAsync(
			new EditEmailProcessDTO
			{
				Id = added.Id,
				CCEmail = "changed@cibi.com.ph",
				IsActive = true
			},
			CancellationToken.None);

		var after = await _emailProcessManagementService.GetEmailProcessesAsync(CancellationToken.None);

		// Assert
		after.Should().ContainSingle(row => row.Id == added.Id)
			.Which.CCEmail.Should().Be("changed@cibi.com.ph");
	}

	[Fact]
	public async Task GetEmailProcessesAsync_ShouldReturnEveryListOrderedByProcess()
	{
		// Arrange - added out of order on purpose.
		await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.Withdrawn),
			CancellationToken.None);
		await _emailProcessManagementService.AddEmailProcessAsync(
			NewCopyList(AtsEmailProcess.ApplicationForm),
			CancellationToken.None);
		await _emailProcessManagementService.AddEmailProcessAsync(
			// Inactive rows are returned too: the screen exists to show a notice whose copy
			// list is switched off, precisely so somebody can switch it back on.
			NewCopyList(AtsEmailProcess.Dispute, ccEmail: string.Empty, isActive: false),
			CancellationToken.None);

		// Act
		var result = await _emailProcessManagementService.GetEmailProcessesAsync(CancellationToken.None);

		// Assert
		result.Select(row => row.EmailProcess).Should().Equal(
			AtsEmailProcess.ApplicationForm,
			AtsEmailProcess.Dispute,
			AtsEmailProcess.Withdrawn);

		result.Should().ContainSingle(row => !row.IsActive);
	}

	#endregion

	#region Seed

	/// <summary>
	/// The production seed never runs in the Testing environment, so this exercises
	/// <c>ATSInitialData.GetEmailProcesses</c> directly - the rows it builds are what a fresh
	/// deployment gets, and nothing else asserts on them.
	/// </summary>
	[Fact]
	public async Task GetEmailProcesses_ShouldSeedOneActiveListPerKnownProcess()
	{
		// Arrange
		var seeded = ATSInitialData.GetEmailProcesses();

		// Act
		await _dbContext.EmailProcessDetails.AddRangeAsync(seeded);
		await _dbContext.SaveChangesAsync();

		var persisted = await _dbContext.EmailProcessDetails
			.AsNoTracking()
			.OrderBy(row => row.EmailProcess)
			.ToListAsync();

		// Assert - one row per process in the constant, none missing.
		persisted.Select(row => row.EmailProcess)
			.Should().BeEquivalentTo(AtsEmailProcess.All);

		persisted.Should().OnlyContain(row => row.IsActive);

		// Every seeded list has to survive the same rules an operator's edit does, or the
		// screen would open on a row it cannot save back.
		persisted.Should().OnlyContain(row => EmailCopyList.Validate(row.CCEmail) == null);

		// The AGREED addresses, deliberately not the tester mailboxes the email constants are
		// currently swapped to - seed data ships to production.
		persisted.Single(row => row.EmailProcess == AtsEmailProcess.Withdrawn)
			.CCEmail.Should().Be("clientsupport@cibi.com.ph");

		persisted.Single(row => row.EmailProcess == AtsEmailProcess.ApplicationForm)
			.CCEmail.Should().Be("clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph");
	}

	#endregion
}
