using ATS.DTO;
using ATS.Features.Web.PackageManagement.Command.AddPackage;
using ATS.Features.Web.PackageManagement.Command.EditPackage;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The 0-90 day bound on a package's follow-up interval.
/// </summary>
/// <remarks>
/// Worth its own tests because the failure mode is silent: the chaser fires on
/// OrderCreatedAt + this interval, so an unbounded 900 does not error anywhere - it just never
/// sends a reminder, and looks identical to "turned off" until three years from now.
/// See docs/ats-package-follow-up-email.md.
/// </remarks>
public class PackageFollowUpValidationTests
{
	private readonly AddPackageCommandValidator _addValidator = new();
	private readonly EditPackageCommandValidator _editValidator = new();

	// 0 is the off switch and the default every package starts with, so it must pass.
	// 90 is the ceiling itself - InclusiveBetween, not exclusive.
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(45)]
	[InlineData(90)]
	public void AddPackage_ShouldAccept_WhenFollowUpIsWithinBounds(int days)
	{
		var result = _addValidator.Validate(new AddPackageCommand(BuildAddPackage(days)));

		result.IsValid.Should().BeTrue();
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(91)]
	[InlineData(900)]
	public void AddPackage_ShouldReject_WhenFollowUpIsOutOfBounds(int days)
	{
		var result = _addValidator.Validate(new AddPackageCommand(BuildAddPackage(days)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.ErrorMessage == "Follow-up must be between 0 and 90 days.");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(90)]
	public void EditPackage_ShouldAccept_WhenFollowUpIsWithinBounds(int days)
	{
		var result = _editValidator.Validate(new EditPackageCommand(BuildEditPackage(days)));

		result.IsValid.Should().BeTrue();
	}

	// The edit path matters as much as the add path: raising an existing package to 900 days
	// disarms the chaser for every order placed under it afterwards.
	[Theory]
	[InlineData(-1)]
	[InlineData(91)]
	public void EditPackage_ShouldReject_WhenFollowUpIsOutOfBounds(int days)
	{
		var result = _editValidator.Validate(new EditPackageCommand(BuildEditPackage(days)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.ErrorMessage == "Follow-up must be between 0 and 90 days.");
	}

	// Every other field valid, so a failure can only be the follow-up rule.
	private static AddPackageDTO BuildAddPackage(int followUpEmail) => new()
	{
		PackageName = "Standard Screening",
		PackageDescription = "Standard background screening package",
		IsActive = true,
		AutoChasing = true,
		FollowUpEmail = followUpEmail
	};

	private static EditPackageDTO BuildEditPackage(int followUpEmail) => new()
	{
		PackageId = 1,
		PackageName = "Standard Screening",
		PackageDescription = "Standard background screening package",
		IsActive = true,
		AutoChasing = true,
		FollowUpEmail = followUpEmail
	};
}
