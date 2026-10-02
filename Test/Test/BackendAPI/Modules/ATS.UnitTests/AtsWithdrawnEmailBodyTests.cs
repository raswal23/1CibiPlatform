using System.Text.RegularExpressions;
using ATS.Configuration;
using ATS.Constants;
using ATS.Services.EmailAccounts;
using ATS.Services.EmailService;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The withdrawal notice body. A pure function, so it is asserted directly rather than through a
/// send - <c>SmtpLease</c> wraps a real <c>SmtpClient</c>, which is why
/// <c>ApplicationFormServiceWithdrawnEmailTests</c> stops at the sender contract.
/// </summary>
public class AtsWithdrawnEmailBodyTests
{
	private const string RequestorName = "Ana Reyes";
	private const string CandidateName = "Juan Dela Cruz";

	private static ATSEmailService Service()
	{
		return new ATSEmailService(
			NullLogger<ATSEmailService>.Instance,
			new Mock<ISmtpAccountPoolRegistry>().Object,
			Options.Create(new AtsEmailDeliveryOptions()));
	}

	[Fact]
	public void BuildWithdrawnApplicationNotification_ShouldAddressTheRequestorAndNameTheCandidate()
	{
		// Act
		var body = Service().BuildWithdrawnApplicationNotification(RequestorName, CandidateName);

		// Assert: the agreed copy, verbatim. The requestor is the one who has to act on this, so
		// the greeting is theirs and the candidate is named inside the sentence.
		body.Should().Contain($"Dear {RequestorName},");
		body.Should().Contain(
			$"Your candidate, {CandidateName}, has withdrawn their Application Form. The background verification should not proceed without the completed Application Form.");
		body.Should().Contain("ccteam@cibi.com.ph");
		body.Should().Contain("clientsupport@cibi.com.ph");
	}

	[Fact]
	public void BuildWithdrawnApplicationNotification_ShouldRepeatTheSubjectAsItsHeader()
	{
		// Act
		var body = Service().BuildWithdrawnApplicationNotification(RequestorName, CandidateName);

		// Assert: read from the same constant the caller sends as the subject line, so the preview
		// and the opened message cannot disagree.
		body.Should().Contain(WithdrawnEmail.Subject);
	}

	[Fact]
	public void BuildWithdrawnApplicationNotification_ShouldEncodeNames_WhenTheyContainMarkup()
	{
		// Arrange: both names come from stored data - the requestor from the Auth directory, the
		// candidate from the invitation row - and land inside markup.
		const string injected = "<script>alert('x')</script>";

		// Act
		var body = Service().BuildWithdrawnApplicationNotification(injected, CandidateName);

		// Assert
		body.Should().NotContain("<script>");
		body.Should().Contain("&lt;script&gt;");
	}

	[Fact]
	public void BuildWithdrawnApplicationNotification_ShouldLinkOnlyToMailboxes()
	{
		// Act
		var body = Service().BuildWithdrawnApplicationNotification(RequestorName, CandidateName);

		// Assert: every other candidate-facing body carries an "Application Form" button, and this
		// one must not - the form is gone, and a link would invite the requestor to reopen
		// something the candidate just closed.
		var hrefs = Regex
			.Matches(body, "href='([^']*)'")
			.Select(match => match.Groups[1].Value)
			.ToList();

		hrefs.Should().NotBeEmpty();
		hrefs.Should().OnlyContain(href => href.StartsWith("mailto:"));
	}
}
