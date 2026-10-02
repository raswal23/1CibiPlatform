using ATS.Configuration;
using ATS.Services.EmailService;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The limiter is what actually keeps this sender under the provider's ceiling, so its
/// behaviour is tested directly rather than only through the processor.
/// </summary>
public class SmtpRateLimiterTests
{
	private static SmtpRateLimiter CreateLimiter(double sendsPerSecond) =>
		new(
			Options.Create(new AtsEmailDeliveryOptions { MaxSendsPerSecond = sendsPerSecond }),
			new Mock<ILogger<SmtpRateLimiter>>().Object);

	[Fact]
	public async Task WaitForSlotAsync_ShouldSpaceSends_AtTheConfiguredRate()
	{
		// Arrange: 20/s means slots are 50ms apart.
		using var limiter = CreateLimiter(sendsPerSecond: 20);

		var stopwatch = Stopwatch.StartNew();

		// Act: five slots span four intervals, so at least 200ms must elapse.
		for (var i = 0; i < 5; i++)
		{
			await limiter.WaitForSlotAsync(CancellationToken.None);
		}

		stopwatch.Stop();

		// Assert: generous lower bound - the point is that the calls were PACED, not that
		// the scheduler is precise. Without the limiter this loop returns in ~0ms.
		stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(150);
	}

	[Fact]
	public async Task WaitForSlotAsync_ShouldHoldTheRate_WhenCallersRunConcurrently()
	{
		// Arrange: this is the property that matters. Concurrency must not be able to
		// outrun the limit - otherwise raising the connection count re-creates the
		// original incident.
		using var limiter = CreateLimiter(sendsPerSecond: 20);

		var stopwatch = Stopwatch.StartNew();

		// Act: eight callers race for slots at once.
		var waits = Enumerable
			.Range(0, 8)
			.Select(_ => limiter.WaitForSlotAsync(CancellationToken.None));

		await Task.WhenAll(waits);

		stopwatch.Stop();

		// Assert: eight slots at 50ms apart is seven intervals - the rate is global, not
		// per caller.
		stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(300);
	}

	[Fact]
	public void ReportThrottled_ShouldMarkTheSenderThrottled()
	{
		// Arrange
		using var limiter = CreateLimiter(sendsPerSecond: 100);

		limiter.IsThrottled.Should().BeFalse();

		// Act
		limiter.ReportThrottled(TimeSpan.FromMinutes(10));

		// Assert
		limiter.IsThrottled.Should().BeTrue();
	}

	[Fact]
	public void ReportThrottled_ShouldNeverShortenAnExistingBackoff()
	{
		// Arrange: two workers hitting the limit at once must not let the second one's
		// shorter window undo the first one's.
		using var limiter = CreateLimiter(sendsPerSecond: 100);

		limiter.ReportThrottled(TimeSpan.FromMinutes(30));

		// Act
		limiter.ReportThrottled(TimeSpan.FromMilliseconds(1));

		// Assert: still throttled, because the longer back-off stands.
		limiter.IsThrottled.Should().BeTrue();
	}

	[Fact]
	public void Constructor_ShouldFallBackToTheDefault_WhenRateIsNotPositive()
	{
		// Arrange: a misconfigured 0 would otherwise mean "never send", which is never what
		// was intended.
		using var limiter = CreateLimiter(sendsPerSecond: 0);

		// Act
		Func<Task> act = async () => await limiter.WaitForSlotAsync(CancellationToken.None);

		// Assert
		act.Should().NotThrowAsync();
	}

	[Fact]
	public void ReportThrottled_ShouldAlsoBlockNewLogins()
	{
		// Arrange: the two budgets share one back-off window. A throttle must stop new
		// LOGINS as well as new sends, or the pool keeps re-authenticating into it.
		using var limiter = CreateLimiter(sendsPerSecond: 100);

		limiter.IsLoginThrottled.Should().BeFalse();

		// Act
		limiter.ReportThrottled(TimeSpan.FromMinutes(30));

		// Assert
		limiter.IsLoginThrottled.Should().BeTrue();
	}

	[Fact]
	public async Task WaitForLoginSlotAsync_ShouldSpaceLogins_IndependentlyOfSends()
	{
		// Arrange: logins are paced on their own budget. This is the fix for "454 Too many
		// login attempts" - a fast send rate must not imply a fast login rate.
		using var limiter = new SmtpRateLimiter(
			Options.Create(new AtsEmailDeliveryOptions
			{
				MaxSendsPerSecond = 1_000,
				MinSecondsBetweenLogins = 1
			}),
			new Mock<ILogger<SmtpRateLimiter>>().Object);

		var stopwatch = Stopwatch.StartNew();

		// Act: two logins, one interval apart.
		await limiter.WaitForLoginSlotAsync(CancellationToken.None);
		await limiter.WaitForLoginSlotAsync(CancellationToken.None);

		stopwatch.Stop();

		// Assert: the second waited, even though the send rate is effectively unlimited.
		stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(800);
	}
}
