using ATS.DTO;
using ATS.Features.Reports.Command.EditSubjectName;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class EditSubjectNameCommandValidatorTests
{
	private readonly EditSubjectNameCommandValidator _validator = new();

	private static EditSubjectNameCommand CreateCommand(
		Guid? invitationId = null,
		string? firstName = "Ada",
		string? middleInitial = null,
		string? lastName = "Lovelace") =>
		new(new EditSubjectNameDTO
		{
			EmailInvitationRequestId = invitationId ?? Guid.CreateVersion7(),
			FirstName = firstName,
			MiddleInitial = middleInitial,
			LastName = lastName
		});

	[Fact]
	public void Validate_ShouldPass_WhenTheNamesAreValid()
	{
		var result = _validator.Validate(CreateCommand(middleInitial: "Byron"));

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Validate_ShouldPass_WhenTheMiddleNameIsOmitted()
	{
		var result = _validator.Validate(CreateCommand(middleInitial: null));

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Validate_ShouldFail_WhenTheSubjectPayloadIsNull()
	{
		var result = _validator.Validate(new EditSubjectNameCommand(null!));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Subject name data is required.");
	}

	[Fact]
	public void Validate_ShouldFail_WhenTheInvitationIdIsEmpty()
	{
		var result = _validator.Validate(CreateCommand(invitationId: Guid.Empty));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Email Invitation ID is required.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldFail_WhenTheFirstNameIsMissing(string? firstName)
	{
		var result = _validator.Validate(CreateCommand(firstName: firstName));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "First name is required.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldFail_WhenTheLastNameIsMissing(string? lastName)
	{
		var result = _validator.Validate(CreateCommand(lastName: lastName));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Last name is required.");
	}

	[Fact]
	public void Validate_ShouldFail_WhenTheFirstNameExceedsTheColumnLength()
	{
		var result = _validator.Validate(CreateCommand(firstName: new string('a', 256)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "First name must not exceed 255 characters.");
	}

	[Fact]
	public void Validate_ShouldFail_WhenTheLastNameExceedsTheColumnLength()
	{
		var result = _validator.Validate(CreateCommand(lastName: new string('a', 256)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Last name must not exceed 255 characters.");
	}

	[Fact]
	public void Validate_ShouldFail_WhenTheMiddleNameExceedsTheColumnLength()
	{
		var result = _validator.Validate(CreateCommand(middleInitial: new string('a', 256)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Middle name must not exceed 255 characters.");
	}
}
