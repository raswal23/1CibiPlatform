using EmploymentVerification.Features.VerifyEmployment.Command.RejectRequest;
using FluentAssertions;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// The decline reason arrives on an anonymous endpoint reached from an emailed link,
/// and that link is single use - so this validator is the only thing standing between
/// "the employer said the details are wrong" and a stored row that says nothing about
/// why. It runs in ValidationBehavior before the handler, so a rejected call never
/// reaches PostgreSQL.
/// </summary>
public class RejectRequestValidatorTests
{
	// 86 characters: the SHA-512 hash from IHashService rendered as unpadded base64url.
	private const string ValidToken =
		"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
		+ "ABCDEFGHIJKLMNOPQRSTUV";

	private static RejectRequestCommand Command(string reason) =>
		new(ValidToken, reason);

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t\r\n")]
	public void Validator_ShouldReject_WhenNoReasonIsGiven(string reason)
	{
		var result = new RejectRequestCommandValidator().Validate(Command(reason));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error =>
			error.PropertyName == nameof(RejectRequestCommand.Reason));
	}

	[Fact]
	public void Validator_ShouldReject_WhenTheReasonExceedsTheStorageCap()
	{
		// The cap exists because an anonymous caller writes this straight into a column
		// the console renders in a table cell.
		var result = new RejectRequestCommandValidator()
			.Validate(Command(new string('x', 1001)));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage.Contains("1000"));
	}

	[Fact]
	public void Validator_ShouldAccept_WhenAReasonIsGiven()
	{
		var result = new RejectRequestCommandValidator()
			.Validate(Command("The employment end date is a year out."));

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Validator_ShouldStillReject_WhenTheTokenIsMalformed()
	{
		// Adding the reason rule must not have displaced the token rules: the token is
		// the credential, and this endpoint is anonymous.
		var result = new RejectRequestCommandValidator()
			.Validate(new RejectRequestCommand("not-a-real-token", "Dates are wrong"));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error =>
			error.PropertyName == nameof(RejectRequestCommand.Token));
	}
}
