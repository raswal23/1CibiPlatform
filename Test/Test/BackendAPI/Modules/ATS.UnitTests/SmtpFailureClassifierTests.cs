using ATS.Services.EmailService;
using FluentAssertions;
using MailKit.Net.Smtp;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The classifier decides two things that used to be wrong: whether a failure is worth
/// retrying, and whether the SMTP SESSION survives it. The second is what caused
/// "454 Too many login attempts" - discarding a healthy session on every failure forced a
/// fresh login, which provoked the next throttle.
/// </summary>
public class SmtpFailureClassifierTests
{
	#region Send failures
	[Fact]
	public void ClassifySendFailure_ShouldKeepTheSession_WhenOneRecipientIsRejected()
	{
		// Arrange: a 550 is about the mailbox, not the connection.
		var exception = new SmtpCommandException(
			SmtpErrorCode.RecipientNotAccepted,
			SmtpStatusCode.MailboxUnavailable,
			"550 No such user here");

		// Act
		var (result, sessionIsUsable) = SmtpFailureClassifier.ClassifySendFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Permanent);

		// The critical assertion. Discarding the session here would force a needless
		// re-login for the very next candidate in the batch.
		sessionIsUsable.Should().BeTrue();
	}

	[Fact]
	public void ClassifySendFailure_ShouldKeepTheSession_WhenThrottledWithoutDisconnect()
	{
		// Arrange: 454 does not close the connection.
		var exception = new SmtpCommandException(
			SmtpErrorCode.MessageNotAccepted,
			(SmtpStatusCode)454,
			"454 4.7.0 Too many login attempts, please try again later");

		// Act
		var (result, sessionIsUsable) = SmtpFailureClassifier.ClassifySendFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Throttled);

		// This is the regression under test: the first version discarded the session on
		// every throttle, so recovering from one 454 opened a new connection and earned
		// another 454.
		sessionIsUsable.Should().BeTrue();
	}

	[Fact]
	public void ClassifySendFailure_ShouldDiscardTheSession_When421ClosesIt()
	{
		// Arrange: 421 means "service closing transmission channel" - the socket is gone.
		var exception = new SmtpCommandException(
			SmtpErrorCode.MessageNotAccepted,
			(SmtpStatusCode)421,
			"421 4.7.0 Try again later, closing connection");

		// Act
		var (result, sessionIsUsable) = SmtpFailureClassifier.ClassifySendFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Throttled);
		sessionIsUsable.Should().BeFalse();
	}

	[Fact]
	public void ClassifySendFailure_ShouldTreat4xxAsTransient()
	{
		// Arrange
		var exception = new SmtpCommandException(
			SmtpErrorCode.MessageNotAccepted,
			(SmtpStatusCode)451,
			"451 Local error in processing");

		// Act
		var (result, sessionIsUsable) = SmtpFailureClassifier.ClassifySendFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Transient);
		sessionIsUsable.Should().BeTrue();
	}
	#endregion

	#region Connect and authenticate failures
	[Fact]
	public void ClassifyConnectFailure_ShouldDetectALoginThrottle()
	{
		// Arrange: this is raised by AuthenticateAsync, which is why it has to be classified
		// at the pool rather than in the send path - it never reaches the send try/catch.
		var exception = new MailKit.Security.AuthenticationException(
			"454 4.7.0 Too many login attempts, please try again later");

		// Act
		var result = SmtpFailureClassifier.ClassifyConnectFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Throttled);
	}

	[Fact]
	public void ClassifyConnectFailure_ShouldTreatBadCredentialsAsPermanent()
	{
		// Arrange: a wrong app password is not a throttle, and retrying it forever is how a
		// misconfiguration looks like an outage.
		var exception = new MailKit.Security.AuthenticationException(
			"535 5.7.8 Username and Password not accepted");

		// Act
		var result = SmtpFailureClassifier.ClassifyConnectFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Permanent);
	}

	[Fact]
	public void ClassifyConnectFailure_ShouldTreatAnUnreachableHostAsTransient()
	{
		// Arrange
		var exception = new System.Net.Sockets.SocketException(10060);

		// Act
		var result = SmtpFailureClassifier.ClassifyConnectFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Transient);
	}

	[Fact]
	public void ClassifyConnectFailure_ShouldDetectAThrottleFromTheHeloStage()
	{
		// Arrange: Gmail's DoS protection answers at HELO, before authentication.
		var exception = new SmtpCommandException(
			SmtpErrorCode.UnexpectedStatusCode,
			(SmtpStatusCode)421,
			"421 4.7.0 Try again later, closing connection");

		// Act
		var result = SmtpFailureClassifier.ClassifyConnectFailure(exception);

		// Assert
		result.Outcome.Should().Be(EmailDeliveryOutcome.Throttled);
	}
	#endregion
}
