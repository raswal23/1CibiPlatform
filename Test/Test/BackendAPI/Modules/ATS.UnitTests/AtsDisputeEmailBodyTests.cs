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
/// The dispute acknowledgement body. A pure function, so it is asserted directly rather than
/// through a send - see <c>AtsWithdrawnEmailBodyTests</c> for why the send cannot be faked.
/// </summary>
public class AtsDisputeEmailBodyTests
{
	private const string FilerName = "Ana Reyes";
	private const string CandidateName = "Ada Lovelace";

	private static ATSEmailService Service()
	{
		return new ATSEmailService(
			NullLogger<ATSEmailService>.Instance,
			new Mock<ISmtpAccountPoolRegistry>().Object,
			Options.Create(new AtsEmailDeliveryOptions()));
	}

	[Fact]
	public void BuildDisputeNotification_ShouldAddressTheFilerAndNameTheCandidate()
	{
		// Act
		var body = Service().BuildDisputeNotification(FilerName, CandidateName, "Report", null);

		// Assert: the agreed copy, verbatim.
		body.Should().Contain($"Dear {FilerName},");
		body.Should().Contain($"A dispute has been submitted for {CandidateName}. Please see the dispute details below:");
		body.Should().Contain(
			"The dispute will be reviewed and processed accordingly through the Applicant Tracking System (ATS).");
		body.Should().Contain("ccteam@cibi.com.ph");
		body.Should().Contain("clientsupport@cibi.com.ph");
	}

	[Fact]
	public void BuildDisputeNotification_ShouldRepeatTheSubjectAsItsHeader()
	{
		// Act
		var body = Service().BuildDisputeNotification(FilerName, CandidateName, "Report", null);

		// Assert: read from the same constant the caller sends as the subject line, so the preview
		// and the opened message cannot disagree.
		body.Should().Contain(DisputeEmail.Subject);
	}

	[Fact]
	public void BuildDisputeNotification_ShouldShowBothLines_WhenDetailsAreGiven()
	{
		// Arrange: an "Others" dispute, the only case where the console captures free text.
		const string details = "The report lists an employer I never worked for.";

		// Act
		var body = Service().BuildDisputeNotification(FilerName, CandidateName, "Others", details);

		// Assert
		body.Should().Contain("Dispute Category:");
		body.Should().Contain("Others");
		body.Should().Contain("Dispute Details:");
		body.Should().Contain(details);
	}

	[Fact]
	public void BuildDisputeNotification_ShouldOmitTheDetailsLine_WhenThereAreNoDetails()
	{
		// Act: a Billing or Report dispute has a category and nothing else.
		var body = Service().BuildDisputeNotification(FilerName, CandidateName, "Billing", null);

		// Assert: the bullet is dropped rather than rendered empty. "Dispute Details:" with nothing
		// after it reads like a value failed to load.
		body.Should().Contain("Dispute Category:");
		body.Should().Contain("Billing");
		body.Should().NotContain("Dispute Details:");
	}

	[Fact]
	public void BuildDisputeNotification_ShouldEncodeValues_WhenTheyContainMarkup()
	{
		// Arrange: the dispute text is typed into the console by an authenticated user and lands
		// inside markup, so it is the one most worth encoding.
		const string injected = "<script>alert('x')</script>";

		// Act
		var body = Service().BuildDisputeNotification(injected, CandidateName, "Others", injected);

		// Assert
		body.Should().NotContain("<script>");
		body.Should().Contain("&lt;script&gt;");
	}

	[Fact]
	public void BuildDisputeNotification_ShouldLinkOnlyToMailboxes()
	{
		// Act
		var body = Service().BuildDisputeNotification(FilerName, CandidateName, "Report", null);

		// Assert: there is nothing here for the filer to act on, so no button - a link would imply
		// the dispute still needs something from them.
		var hrefs = Regex
			.Matches(body, "href='([^']*)'")
			.Select(match => match.Groups[1].Value)
			.ToList();

		hrefs.Should().NotBeEmpty();
		hrefs.Should().OnlyContain(href => href.StartsWith("mailto:"));
	}
}
