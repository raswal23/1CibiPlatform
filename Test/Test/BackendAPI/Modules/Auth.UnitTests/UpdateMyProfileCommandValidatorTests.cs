using Auth.DTO;
using Auth.Features.UserProfile.Command.UpdateMyProfile;
using FluentAssertions;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class UpdateMyProfileCommandValidatorTests
{
	private readonly UpdateMyProfileCommandValidator _validator = new();

	private static UpdateMyProfileCommand CreateCommand(
		string? firstName = "John",
		string? middleName = null,
		string? lastName = "Doe") =>
		new(new UpdateUserProfileDTO
		{
			FirstName = firstName,
			MiddleName = middleName,
			LastName = lastName
		});

	[Fact]
	public void Validate_ShouldPass_WhenNamesAreValid()
	{
		var result = _validator.Validate(CreateCommand(middleName: "Quincy"));

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Validate_ShouldPass_WhenMiddleNameIsOmitted()
	{
		var result = _validator.Validate(CreateCommand(middleName: null));

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Validate_ShouldFail_WhenProfileIsNull()
	{
		var result = _validator.Validate(new UpdateMyProfileCommand(null!));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Profile data is required.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldFail_WhenFirstNameIsMissing(string? firstName)
	{
		var result = _validator.Validate(CreateCommand(firstName: firstName));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "First name is required.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldFail_WhenLastNameIsMissing(string? lastName)
	{
		var result = _validator.Validate(CreateCommand(lastName: lastName));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Last name is required.");
	}

	[Fact]
	public void Validate_ShouldFail_WhenFirstNameExceedsColumnLength()
	{
		var result = _validator.Validate(CreateCommand(firstName: new string('a', 101)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "First name must not exceed 100 characters.");
	}

	[Fact]
	public void Validate_ShouldFail_WhenLastNameExceedsColumnLength()
	{
		var result = _validator.Validate(CreateCommand(lastName: new string('a', 101)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Last name must not exceed 100 characters.");
	}

	[Fact]
	public void Validate_ShouldFail_WhenMiddleNameExceedsColumnLength()
	{
		var result = _validator.Validate(CreateCommand(middleName: new string('a', 101)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Middle name must not exceed 100 characters.");
	}
}
