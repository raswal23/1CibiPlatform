using ATS.DTO;
using ATS.Services.OrderValidation;
using ATS.Services.Settings.PackageManagement;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class OrderInputValidatorTests
{
	private const int ClientId = 77;
	private const string AssignedPackage = "CRIMINAL RECORDS CHECK";

	private readonly Mock<IPackageManagementService> _packageService = new();
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly OrderInputValidator _validator;

	public OrderInputValidatorTests()
	{
		_currentUser.Setup(user => user.AtsClientId).Returns(ClientId);

		GivenAssignedPackages(
			new PackageDetailsDTO { PackageId = 1, PackageName = AssignedPackage, IsActive = true });

		_validator = new OrderInputValidator(_packageService.Object, _currentUser.Object);
	}

	private void GivenAssignedPackages(params PackageDetailsDTO[] packages) =>
		_packageService
			.Setup(service => service.GetPackagesAsync(
				It.IsAny<KeysetPaginationRequest>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<int?>()))
			.ReturnsAsync(new KeysetPaginatedResult<PackageDetailsDTO>(packages, null, packages.Length));

	[Fact]
	public async Task ValidateAsync_ShouldAccept_AnAssignedPackageAndAKnownOrderType()
	{
		var result = await _validator.ValidateAsync(AssignedPackage, "Normal", CancellationToken.None);

		result.Package.Should().Be(AssignedPackage);
		result.OrderType.Should().Be("Normal");
	}

	// The stored spelling is returned, not the caller's: OMS ticketing matches the
	// package by name, so a casing difference would stop it resolving.
	[Theory]
	[InlineData("criminal records check")]
	[InlineData("Criminal Records Check")]
	[InlineData("  CRIMINAL RECORDS CHECK  ")]
	public async Task ValidateAsync_ShouldReturnTheStoredPackageSpelling(string written)
	{
		var result = await _validator.ValidateAsync(written, "Normal", CancellationToken.None);

		result.Package.Should().Be(AssignedPackage);
	}

	[Theory]
	[InlineData("rush", "Rush")]
	[InlineData("RUSH", "Rush")]
	[InlineData("  normal  ", "Normal")]
	public async Task ValidateAsync_ShouldCanonicalizeTheOrderType(string written, string expected)
	{
		var result = await _validator.ValidateAsync(AssignedPackage, written, CancellationToken.None);

		result.OrderType.Should().Be(expected);
	}

	// The reported bug: any text was accepted as an order type.
	[Theory]
	[InlineData("Whenever")]
	[InlineData("Express")]
	[InlineData("banana")]
	[InlineData("")]
	[InlineData(null)]
	public async Task ValidateAsync_ShouldReject_AnUnknownOrderType(string? orderType)
	{
		var act = async () => await _validator.ValidateAsync(AssignedPackage, orderType, CancellationToken.None);

		var exception = await act.Should().ThrowAsync<BadRequestException>();

		// The message names what is acceptable so the caller can correct the request.
		exception.Which.Message.Should().Contain("Normal").And.Contain("Rush");
	}

	// The other half of the bug: any text was accepted as a package.
	[Fact]
	public async Task ValidateAsync_ShouldReject_APackageTheClientIsNotAssigned()
	{
		var act = async () => await _validator.ValidateAsync("banana", "Normal", CancellationToken.None);

		var exception = await act.Should().ThrowAsync<BadRequestException>();

		exception.Which.Message.Should().Contain("banana");

		// Lists the client's own packages, which they can already read from GET /packages.
		exception.Which.Message.Should().Contain(AssignedPackage);
	}

	[Fact]
	public async Task ValidateAsync_ShouldReject_AnInactivePackage()
	{
		GivenAssignedPackages(
			new PackageDetailsDTO { PackageId = 2, PackageName = "RETIRED CHECK", IsActive = false });

		var act = async () => await _validator.ValidateAsync("RETIRED CHECK", "Normal", CancellationToken.None);

		await act.Should().ThrowAsync<BadRequestException>();
	}

	[Fact]
	public async Task ValidateAsync_ShouldReject_WhenTheClientHasNoPackagesAtAll()
	{
		GivenAssignedPackages();

		var act = async () => await _validator.ValidateAsync(AssignedPackage, "Normal", CancellationToken.None);

		var exception = await act.Should().ThrowAsync<BadRequestException>();

		exception.Which.Message.Should().Contain("No screening package is assigned");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public async Task ValidateAsync_ShouldReject_AMissingPackage(string? package)
	{
		var act = async () => await _validator.ValidateAsync(package, "Normal", CancellationToken.None);

		await act.Should().ThrowAsync<BadRequestException>();
	}

	[Fact]
	public async Task ValidateAsync_ShouldScopeToTheCallersClient_NotARequestSuppliedId()
	{
		await _validator.ValidateAsync(AssignedPackage, "Normal", CancellationToken.None);

		// The client id must come from the token, or one client could order against
		// another's entitlements.
		_packageService.Verify(
			service => service.GetPackagesAsync(
				It.IsAny<KeysetPaginationRequest>(),
				It.IsAny<CancellationToken>(),
				ClientId),
			Times.Once);
	}

	[Fact]
	public async Task ValidateAsync_ShouldRejectTheOrderType_BeforeReadingPackages()
	{
		var act = async () => await _validator.ValidateAsync(AssignedPackage, "Whenever", CancellationToken.None);

		await act.Should().ThrowAsync<BadRequestException>();

		// No database round trip for a value that needs none.
		_packageService.Verify(
			service => service.GetPackagesAsync(
				It.IsAny<KeysetPaginationRequest>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<int?>()),
			Times.Never);
	}
}
