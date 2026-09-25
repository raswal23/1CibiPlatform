using ATS.Constants;
using ATS.DTO;
using ATS.Features.Web.EmailProcessManagement.Command.AddEmailProcess;
using ATS.Features.Web.EmailProcessManagement.Command.EditEmailProcess;
using ATS.Shared;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The copy list lives in ONE varchar column, so the unique index and the length limit say
/// nothing about what is inside it - a duplicated address, a malformed mailbox and stray
/// whitespace are all a valid varchar(1000). These validators are the only thing that rejects
/// them, which is why they are tested here rather than left to an integration run.
/// </summary>
public class EmailProcessValidationTests
{
	private readonly AddEmailProcessCommandValidator _addValidator = new();
	private readonly EditEmailProcessCommandValidator _editValidator = new();

	private static AddEmailProcessCommand AddCommand(
		string emailProcess = AtsEmailProcess.Withdrawn,
		string ccEmail = "clientsupport@cibi.com.ph",
		bool isActive = true) =>
		new(new AddEmailProcessDTO
		{
			EmailProcess = emailProcess,
			CCEmail = ccEmail,
			IsActive = isActive
		});

	private static EditEmailProcessCommand EditCommand(
		int id = 1,
		string ccEmail = "clientsupport@cibi.com.ph",
		bool isActive = true) =>
		new(new EditEmailProcessDTO
		{
			Id = id,
			CCEmail = ccEmail,
			IsActive = isActive
		});

	#region EmailProcess

	[Theory]
	[InlineData(AtsEmailProcess.Withdrawn)]
	[InlineData(AtsEmailProcess.Dispute)]
	[InlineData(AtsEmailProcess.ApplicationForm)]
	[InlineData(AtsEmailProcess.FollowUp)]
	public void AddValidator_ShouldAcceptEveryKnownProcess(string emailProcess)
	{
		var result = _addValidator.Validate(AddCommand(emailProcess: emailProcess));

		result.IsValid.Should().BeTrue();
	}

	// A process outside the constant would be a copy list no notice ever reads: the send path
	// looks a row up by this exact string, so nothing downstream would report it as a mistake.
	[Theory]
	[InlineData("Withdrawal")]
	[InlineData("withdrawn")]
	[InlineData("Application Form")]
	public void AddValidator_ShouldRejectUnknownProcess(string emailProcess)
	{
		var result = _addValidator.Validate(AddCommand(emailProcess: emailProcess));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error =>
			error.ErrorMessage.StartsWith("EmailProcess must be one of:"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void AddValidator_ShouldRejectMissingProcess(string emailProcess)
	{
		var result = _addValidator.Validate(AddCommand(emailProcess: emailProcess));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "EmailProcess is required.");
	}

	#endregion

	#region Copy list contents

	[Theory]
	[InlineData("clientsupport@cibi.com.ph")]
	[InlineData("clientsupport@cibi.com.ph,pre-workteam@cibi.com.ph")]
	// Hand-typed spacing is accepted and normalised away on write rather than rejected - the
	// operator typed a list that means exactly what they intended.
	[InlineData("clientsupport@cibi.com.ph, pre-workteam@cibi.com.ph")]
	public void Validators_ShouldAcceptAWellFormedList(string ccEmail)
	{
		_addValidator.Validate(AddCommand(ccEmail: ccEmail)).IsValid.Should().BeTrue();
		_editValidator.Validate(EditCommand(ccEmail: ccEmail)).IsValid.Should().BeTrue();
	}

	[Theory]
	[InlineData("not-an-email", "'not-an-email' is not a valid email address.")]
	[InlineData("clientsupport@cibi.com.ph,broken@", "'broken@' is not a valid email address.")]
	// No dot in the host - IsValidEmail requires one, so "a@localhost" is not deliverable mail.
	[InlineData("a@localhost", "'a@localhost' is not a valid email address.")]
	public void Validators_ShouldRejectAMalformedAddress(string ccEmail, string expectedMessage)
	{
		_addValidator.Validate(AddCommand(ccEmail: ccEmail)).Errors
			.Should().Contain(error => error.ErrorMessage == expectedMessage);

		_editValidator.Validate(EditCommand(ccEmail: ccEmail)).Errors
			.Should().Contain(error => error.ErrorMessage == expectedMessage);
	}

	// A repeat copies the person twice and is charged twice against the sending account's
	// daily cap. The unique index cannot see it: the whole list is one distinct string.
	[Theory]
	[InlineData("a@cibi.com.ph,a@cibi.com.ph")]
	[InlineData("a@cibi.com.ph,b@cibi.com.ph,A@CIBI.COM.PH")]
	public void Validators_ShouldRejectADuplicateAddress(string ccEmail)
	{
		_addValidator.Validate(AddCommand(ccEmail: ccEmail)).Errors
			.Should().Contain(error => error.ErrorMessage.Contains("appears more than once"));

		_editValidator.Validate(EditCommand(ccEmail: ccEmail)).Errors
			.Should().Contain(error => error.ErrorMessage.Contains("appears more than once"));
	}

	[Fact]
	public void Validators_ShouldRejectAListPastTheColumnWidth()
	{
		// Each entry is unique and well-formed, so the only thing wrong is the total length.
		var ccEmail = string.Join(
			',',
			Enumerable.Range(0, 40).Select(index => $"recipient{index}@somewhatlongdomainname.com.ph"));

		ccEmail.Length.Should().BeGreaterThan(EmailCopyList.MaxLength);

		_addValidator.Validate(AddCommand(ccEmail: ccEmail)).Errors
			.Should().Contain(error =>
				error.ErrorMessage == $"The copy list cannot exceed {EmailCopyList.MaxLength} characters.");
	}

	[Fact]
	public void Validators_ShouldRejectAnOverLongSingleAddress()
	{
		var ccEmail = $"{new string('a', EmailCopyList.MaxAddressLength)}@cibi.com.ph";

		_addValidator.Validate(AddCommand(ccEmail: ccEmail)).Errors
			.Should().Contain(error =>
				error.ErrorMessage ==
				$"An email address in the copy list cannot exceed {EmailCopyList.MaxAddressLength} characters.");
	}

	#endregion

	#region Empty list

	// Registering an empty list is allowed - it just cannot be switched on. An active row with
	// no addresses is the one combination that breaks at SEND time instead of here: the send
	// path would hand "" to MimeKit.MailboxAddress.Parse, which throws for the whole notice.
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(",,")]
	public void Validators_ShouldAcceptAnEmptyListWhenInactive(string ccEmail)
	{
		_addValidator.Validate(AddCommand(ccEmail: ccEmail, isActive: false)).IsValid.Should().BeTrue();
		_editValidator.Validate(EditCommand(ccEmail: ccEmail, isActive: false)).IsValid.Should().BeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(",,")]
	public void Validators_ShouldRejectAnEmptyListWhenActive(string ccEmail)
	{
		_addValidator.Validate(AddCommand(ccEmail: ccEmail, isActive: true)).Errors
			.Should().Contain(error =>
				error.ErrorMessage == "A copy list with no email addresses cannot be active.");

		_editValidator.Validate(EditCommand(ccEmail: ccEmail, isActive: true)).Errors
			.Should().Contain(error =>
				error.ErrorMessage == "A copy list with no email addresses cannot be active.");
	}

	#endregion

	#region Id

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void EditValidator_ShouldRejectAMissingId(int id)
	{
		var result = _editValidator.Validate(EditCommand(id: id));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage == "Id is required.");
	}

	#endregion
}
