using Auth.Constants;
using Auth.Shared.Implementations;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class CurrentUserTests
{
	[Fact]
	public void CurrentUser_ShouldParseStandardAndAtsClaims()
	{
		var userId = Guid.CreateVersion7();
		var context = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
				new Claim(ClaimTypes.Email, "user@example.com"),
				new Claim(ClaimTypes.Name, "Test User"),
				new Claim(AuthClaimTypes.PlatformRoleId, "1"),
				new Claim(AuthClaimTypes.PlatformRoleId, "2"),
				new Claim(AuthClaimTypes.PlatformRoleId, "1"),
				new Claim(AuthClaimTypes.AtsRoleId, "2"),
				new Claim(AuthClaimTypes.AtsClientId, "42")
			],
			"TestAuth"))
		};
		var currentUser = new CurrentUser(new HttpContextAccessor { HttpContext = context });

		currentUser.IsAuthenticated.Should().BeTrue();
		currentUser.UserId.Should().Be(userId);
		currentUser.Email.Should().Be("user@example.com");
		currentUser.FullName.Should().Be("Test User");
		currentUser.PlatformRoleIds.Should().BeEquivalentTo([1, 2]);
		currentUser.IsPlatformSuperAdmin.Should().BeTrue();
		currentUser.AtsRoleId.Should().Be(2);
		currentUser.AtsClientId.Should().Be(42);
	}

	[Fact]
	public void CurrentUser_ShouldReturnNull_ForMissingOrMalformedClaims()
	{
		var context = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AuthClaimTypes.UserId, "not-a-guid"),
				new Claim(AuthClaimTypes.PlatformRoleId, "10"),
				new Claim(AuthClaimTypes.PlatformRoleId, "11"),
				new Claim(AuthClaimTypes.PlatformRoleId, "21"),
				new Claim(AuthClaimTypes.PlatformRoleId, "invalid"),
				new Claim(AuthClaimTypes.AtsRoleId, "invalid"),
				new Claim(AuthClaimTypes.AtsClientId, "0")
			]))
		};
		var currentUser = new CurrentUser(new HttpContextAccessor { HttpContext = context });

		currentUser.IsAuthenticated.Should().BeFalse();
		currentUser.UserId.Should().BeNull();
		currentUser.PlatformRoleIds.Should().BeEquivalentTo([10, 11, 21]);
		currentUser.IsPlatformSuperAdmin.Should().BeFalse();
		currentUser.AtsRoleId.Should().BeNull();
		currentUser.AtsClientId.Should().BeNull();
	}

	[Fact]
	public void CurrentUser_ShouldHandleMissingHttpContext()
	{
		var currentUser = new CurrentUser(new HttpContextAccessor());

		currentUser.IsAuthenticated.Should().BeFalse();
		currentUser.UserId.Should().BeNull();
		currentUser.Email.Should().BeNull();
		currentUser.FullName.Should().BeNull();
		currentUser.PlatformRoleIds.Should().BeEmpty();
		currentUser.IsPlatformSuperAdmin.Should().BeFalse();
		currentUser.AtsRoleId.Should().BeNull();
		currentUser.AtsClientId.Should().BeNull();
	}
}
