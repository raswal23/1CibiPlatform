using ATS.Data.Repository;
using ATS.Services.AccessScope;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class AtsActiveUserGuardTests
{
	private readonly Mock<ICurrentUser> _currentUser = new();
	private readonly Mock<IATSUserRepository> _userRepository = new();

	private AtsActiveUserGuard CreateGuard() =>
		new(_currentUser.Object, _userRepository.Object);

	[Fact]
	public async Task EnsureActiveAtsUserAsync_ShouldBypassWithoutRepositoryCall_ForPlatformSuperAdmin()
	{
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(true);

		await CreateGuard().EnsureActiveAtsUserAsync(CancellationToken.None);

		_userRepository.Verify(
			repository => repository.GetActiveUserRoleIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("00000000-0000-0000-0000-000000000000")]
	public async Task EnsureActiveAtsUserAsync_ShouldThrowForbidden_WhenUserIdIsMissing(string? userId)
	{
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(false);
		_currentUser.SetupGet(user => user.UserId).Returns(userId is null ? null : Guid.Parse(userId));

		Func<Task> act = () => CreateGuard().EnsureActiveAtsUserAsync(CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>()
			.WithMessage("The current user does not have valid ATS access.");
	}

	[Fact]
	public async Task EnsureActiveAtsUserAsync_ShouldPass_WhenUserHasActiveRoles()
	{
		var userId = Guid.CreateVersion7();
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(false);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_userRepository
			.Setup(repository => repository.GetActiveUserRoleIdsAsync(userId, It.IsAny<CancellationToken>()))
			.ReturnsAsync([2]);

		Func<Task> act = () => CreateGuard().EnsureActiveAtsUserAsync(CancellationToken.None);

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task EnsureActiveAtsUserAsync_ShouldThrowForbidden_WhenUserHasNoActiveRoles()
	{
		var userId = Guid.CreateVersion7();
		_currentUser.SetupGet(user => user.IsPlatformSuperAdmin).Returns(false);
		_currentUser.SetupGet(user => user.UserId).Returns(userId);
		_userRepository
			.Setup(repository => repository.GetActiveUserRoleIdsAsync(userId, It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		Func<Task> act = () => CreateGuard().EnsureActiveAtsUserAsync(CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>()
			.WithMessage("The current user does not have valid ATS access.");
	}
}
