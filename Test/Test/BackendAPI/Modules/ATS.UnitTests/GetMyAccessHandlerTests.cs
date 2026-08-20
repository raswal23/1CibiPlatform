using ATS.Features.UserManagement.Query.GetMyAccess;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class GetMyAccessHandlerTests
{
	[Theory]
	[InlineData(1, null)]
	[InlineData(2, 42)]
	[InlineData(3, 42)]
	[InlineData(4, 42)]
	public async Task Handle_ShouldReturnAccess_ForPositiveAtsRole(int roleId, int? clientId)
	{
		var currentUser = new Mock<ICurrentUser>();
		currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
		currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		currentUser.SetupGet(user => user.AtsClientId).Returns(clientId);
		var handler = new GetMyAccessHandler(currentUser.Object);

		var result = await handler.Handle(new GetMyAccessQuery(), CancellationToken.None);

		result.RoleId.Should().Be(roleId);
		result.ClientId.Should().Be(clientId);
	}

	[Theory]
	[InlineData(false, null)]
	[InlineData(true, null)]
	[InlineData(true, 0)]
	public async Task Handle_ShouldRejectMissingAtsAccess(bool isAuthenticated, int? roleId)
	{
		var currentUser = new Mock<ICurrentUser>();
		currentUser.SetupGet(user => user.IsAuthenticated).Returns(isAuthenticated);
		currentUser.SetupGet(user => user.AtsRoleId).Returns(roleId);
		var handler = new GetMyAccessHandler(currentUser.Object);

		Func<Task> act = () => handler.Handle(new GetMyAccessQuery(), CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>()
			.WithMessage("The current user does not have valid ATS access.");
	}
}
