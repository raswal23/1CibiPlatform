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
/// The completed-form notice body. A pure function, so it is asserted directly rather than through
/// a send - <c>SmtpLease</c> wraps a real <c>SmtpClient</c>.
/// </summary>
public class AtsSubmittedFormEmailBodyTests
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
	public void BuildSubmittedFormNotification_ShouldAddressTheRequestorAndNameTheCandidate()
	{
		// Act
		var body = Service().BuildSubmittedFormNotification(RequestorName, CandidateName);

		// Assert: the agreed copy, verbatim.
		body.Should().Contain($"Dear {RequestorName},");
		body.Should().Contain(
			$"Your candidate, {CandidateName}, has successfully completed the Application Form. The completed form is now available for download through the Applicant Tracking System (ATS).");
		body.Should().Contain("ccteam@cibi.com.ph");
		body.Should().Contain("clientsupport@cibi.com.ph");
	}

	[Fact]
	public void BuildSubmittedFormNotification_ShouldRepeatTheSubjectAsItsHeader()
	{
		// Act
		var body = Service().BuildSubmittedFormNotification(RequestorName, CandidateName);

		// Assert: read from the same constant the caller sends as the subject line, so the preview
		// and the opened message cannot disagree.
		body.Should().Contain(SubmittedFormEmail.Subject);
	}

	[Fact]
	public void BuildSubmittedFormNotification_ShouldEncodeNames_WhenTheyContainMarkup()
	{
		// Arrange: the candidate name is typed into the form by an unauthenticated visitor holding
		// an emailed link, so it is the value here most worth encoding.
		const string injected = "<script>alert('x')</script>";

		// Act
		var body = Service().BuildSubmittedFormNotification(RequestorName, injected);

		// Assert
		body.Should().NotContain("<script>");
		body.Should().Contain("&lt;script&gt;");
	}

	[Fact]
	public void BuildSubmittedFormNotification_ShouldLinkOnlyToMailboxes()
	{
		// Act
		var body = Service().BuildSubmittedFormNotification(RequestorName, CandidateName);

		// Assert: the copy mentions a download but deliberately carries no link - the console the
		// requestor already signs into is the download location, and there is no candidate-facing
		// URL that would be safe to put in a message copied to two internal teams.
		var hrefs = Regex
			.Matches(body, "href='([^']*)'")
			.Select(match => match.Groups[1].Value)
			.ToList();

		hrefs.Should().NotBeEmpty();
		hrefs.Should().OnlyContain(href => href.StartsWith("mailto:"));
	}
}
