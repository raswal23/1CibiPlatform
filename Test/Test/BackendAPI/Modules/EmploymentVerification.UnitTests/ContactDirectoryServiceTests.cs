using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using EmploymentVerification.Data.Entities;
using EmploymentVerification.Data.Repository.ContactDirectory;
using EmploymentVerification.DTO;
using EmploymentVerification.Services.ContactDirectory;
using FluentAssertions;
using Moq;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// Covers the three things the contact directory service owns that the repository and
/// the validator do not: normalisation, the composite keyset cursor, and the duplicate
/// guard that turns a would-be constraint violation into a 409.
/// </summary>
public class ContactDirectoryServiceTests
{
	private readonly Mock<IContactDirectoryRepository> _repository = new(MockBehavior.Strict);

	private ContactDirectoryService CreateSut() => new(_repository.Object);

	private static EmploymentVerificationContactDTO Contact(
		string companyName,
		string emailAddress,
		Guid? id = null) =>
		new(
			id ?? Guid.NewGuid(),
			companyName,
			emailAddress,
			true,
			DateTime.UtcNow,
			DateTime.UtcNow);

	[Fact]
	public async Task AddContactAsync_ShouldNormalizeCompanyAndEmail_WhenInputHasWhitespaceAndCase()
	{
		EmploymentVerificationContact? captured = null;

		_repository
			.Setup(repository => repository.ContactExistsAsync(
				"CONCENTRIX",
				"hr@concentrix.com",
				null,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		_repository
			.Setup(repository => repository.AddContactAsync(
				It.IsAny<EmploymentVerificationContact>(),
				It.IsAny<CancellationToken>()))
			.Callback<EmploymentVerificationContact, CancellationToken>(
				(contact, _) => captured = contact)
			.ReturnsAsync(true);

		var result = await CreateSut().AddContactAsync(
			new AddEmploymentVerificationContactDTO("  CONCENTRIX  ", "  HR@Concentrix.COM ", true),
			CancellationToken.None);

		result.Should().BeTrue();
		captured.Should().NotBeNull();
		captured!.CompanyName.Should().Be("CONCENTRIX");

		// Lower-cased because the unique index is a plain composite index on the stored
		// values, so case folding has to happen before the row is written.
		captured.EmailAddress.Should().Be("hr@concentrix.com");
		captured.CreatedAt.Should().Be(captured.UpdatedAt);
	}

	[Fact]
	public async Task AddContactAsync_ShouldThrowConflict_WhenPairIsAlreadyListed()
	{
		_repository
			.Setup(repository => repository.ContactExistsAsync(
				"ACME",
				"hr@acme.test",
				null,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		var act = () => CreateSut().AddContactAsync(
			new AddEmploymentVerificationContactDTO("ACME", "hr@acme.test", true),
			CancellationToken.None);

		await act.Should().ThrowAsync<ConflictException>()
			.WithMessage("*hr@acme.test*ACME*");

		_repository.Verify(
			repository => repository.AddContactAsync(
				It.IsAny<EmploymentVerificationContact>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task EditContactAsync_ShouldExcludeItself_WhenCheckingForDuplicates()
	{
		var id = Guid.NewGuid();

		_repository
			.Setup(repository => repository.GetContactAsync(id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new EmploymentVerificationContact
			{
				Id = id,
				CompanyName = "ACME",
				EmailAddress = "hr@acme.test",
				IsActive = true
			});

		// The row being edited must be excluded, or renaming a contact without changing
		// its identity would collide with itself.
		_repository
			.Setup(repository => repository.ContactExistsAsync(
				"ACME",
				"hr@acme.test",
				id,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		_repository
			.Setup(repository => repository.UpdateContactAsync(
				It.IsAny<EmploymentVerificationContact>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		var result = await CreateSut().EditContactAsync(
			new EditEmploymentVerificationContactDTO(id, "ACME", "hr@acme.test", false),
			CancellationToken.None);

		result.Should().BeTrue();

		_repository.Verify(
			repository => repository.UpdateContactAsync(
				It.Is<EmploymentVerificationContact>(contact => !contact.IsActive),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task EditContactAsync_ShouldThrowNotFound_WhenContactIsGone()
	{
		var id = Guid.NewGuid();

		_repository
			.Setup(repository => repository.GetContactAsync(id, It.IsAny<CancellationToken>()))
			.ReturnsAsync((EmploymentVerificationContact?)null);

		var act = () => CreateSut().EditContactAsync(
			new EditEmploymentVerificationContactDTO(id, "ACME", "hr@acme.test", true),
			CancellationToken.None);

		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task GetContactsAsync_ShouldMintCompositeCursorAndCount_WhenOnTheFirstPage()
	{
		var lastId = Guid.NewGuid();

		// pageSize + 1 rows: the extra one only signals that a next page exists.
		_repository
			.Setup(repository => repository.GetContactsPageAsync(
				null,
				null,
				null,
				3,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(
			[
				Contact("ALPHA", "a@alpha.test"),
				Contact("BETA", "b@beta.test", lastId),
				Contact("GAMMA", "g@gamma.test")
			]);

		_repository
			.Setup(repository => repository.CountContactsAsync(null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(42);

		var result = await CreateSut().GetContactsAsync(
			new KeysetPaginationRequest(null, 2),
			CancellationToken.None);

		result.Items.Should().HaveCount(2);
		result.TotalCount.Should().Be(42);

		// Composite because company name is not unique; the id breaks the tie.
		CursorCodec.Decode(result.NextCursor, 2)
			.Should().BeEquivalentTo(["BETA", lastId.ToString()]);
	}

	[Fact]
	public async Task GetContactsAsync_ShouldOmitTheCount_WhenWalkingACursorPage()
	{
		var cursor = CursorCodec.Encode("BETA", Guid.NewGuid().ToString());

		_repository
			.Setup(repository => repository.GetContactsPageAsync(
				null,
				"BETA",
				It.IsAny<Guid?>(),
				3,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([Contact("GAMMA", "g@gamma.test")]);

		var result = await CreateSut().GetContactsAsync(
			new KeysetPaginationRequest(cursor, 2),
			CancellationToken.None);

		result.Items.Should().HaveCount(1);

		// Counting on every page would cost a full scan per click; the caller reuses the
		// value captured on page one.
		result.TotalCount.Should().BeNull();

		// No next cursor: fewer rows came back than pageSize + 1, so this is the last page.
		result.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task GetContactsAsync_ShouldFallBackToTheFirstPage_WhenTheCursorIsUndecodable()
	{
		// A malformed or stale cursor is never an error - CursorCodec returns null and the
		// caller silently starts again, which is also what happens if a company name
		// acquires a '|' after the cursor was minted.
		_repository
			.Setup(repository => repository.GetContactsPageAsync(
				null,
				null,
				null,
				3,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([Contact("ALPHA", "a@alpha.test")]);

		_repository
			.Setup(repository => repository.CountContactsAsync(null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(1);

		var result = await CreateSut().GetContactsAsync(
			new KeysetPaginationRequest("not-a-real-cursor", 2),
			CancellationToken.None);

		result.Items.Should().ContainSingle();
		result.TotalCount.Should().Be(1);
	}

	[Fact]
	public async Task GetContactsAsync_ShouldClampPageSize_WhenCallerAsksForMoreThanTheMaximum()
	{
		_repository
			.Setup(repository => repository.GetContactsPageAsync(
				null,
				null,
				null,
				KeysetPage.MaxPageSize + 1,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		_repository
			.Setup(repository => repository.CountContactsAsync(null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(0);

		var result = await CreateSut().GetContactsAsync(
			new KeysetPaginationRequest(null, 5_000),
			CancellationToken.None);

		result.Items.Should().BeEmpty();
		_repository.VerifyAll();
	}
}
