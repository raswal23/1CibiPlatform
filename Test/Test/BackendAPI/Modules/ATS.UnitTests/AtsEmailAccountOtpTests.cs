using ATS.Constants;
using ATS.DTO;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The one-time code that stands between a typed password and the send queue.
/// </summary>
/// <remarks>
/// Two properties are being defended. A code is a limited number of guesses, not a million cheap
/// ones - so a wrong answer costs an attempt and the fifth consumes the code entirely. And
/// nothing is applied until the right code arrives: an account stays Pending, an edit stays parked
/// on the OTP row, and a delete removes nothing.
/// </remarks>
public class AtsEmailAccountOtpTests
{
	private static VerifyEmailAccountOtpDTO Submission(string code, string purpose) => new()
	{
		AtsEmailAccountId = 1,
		Purpose = purpose,
		OtpCode = code
	};

	[Fact]
	public async Task VerifyOtpAsync_ShouldIncrementAttempts_WhenTheCodeIsWrong()
	{
		// Arrange
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.ExistingAccount();

		var otp = fixture.HasActiveOtp(1, AtsEmailAccountOtpPurpose.Register);

		// Act
		var result = await fixture.Service.VerifyOtpAsync(
			Submission("000000", AtsEmailAccountOtpPurpose.Register),
			CancellationToken.None);

		// Assert
		result.IsVerified.Should().BeFalse();
		otp.AttemptCount.Should().Be(1);
		otp.IsUsed.Should().BeFalse();

		// Counted down rather than hidden, so an operator sees the cap closing instead of meeting
		// a sudden "no longer valid" on the fifth try.
		result.RemainingAttempts.Should().Be(AtsEmailAccountOtpPolicy.MaxAttempts - 1);
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldNotApplyAnything_WhenTheCodeIsWrong()
	{
		// Arrange: the account must still be Pending afterwards. A wrong code that admitted the
		// account to rotation would make the whole flow decorative.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.ExistingAccount(status: AtsEmailAccountStatus.Pending);
		fixture.HasActiveOtp(1, AtsEmailAccountOtpPurpose.Register);

		// Act
		await fixture.Service.VerifyOtpAsync(
			Submission("999999", AtsEmailAccountOtpPurpose.Register),
			CancellationToken.None);

		// Assert
		fixture.UpdatedAccounts.Should().BeEmpty();
		fixture.DeletedAccounts.Should().BeEmpty();
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldConsumeTheCode_OnTheFifthWrongAttempt()
	{
		// Arrange: four already spent. Consuming rather than locking the account is deliberate -
		// the account is not trusted yet, so there is nothing to lock, and a resend costs one email.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.ExistingAccount();

		var otp = fixture.HasActiveOtp(
			1,
			AtsEmailAccountOtpPurpose.Register,
			attemptCount: AtsEmailAccountOtpPolicy.MaxAttempts - 1);

		// Act
		var result = await fixture.Service.VerifyOtpAsync(
			Submission("000000", AtsEmailAccountOtpPurpose.Register),
			CancellationToken.None);

		// Assert
		result.IsVerified.Should().BeFalse();
		result.RemainingAttempts.Should().Be(0);
		otp.IsUsed.Should().BeTrue();
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldRefuse_WhenNoCodeIsOutstanding()
	{
		// Arrange: expired, already used and never issued all arrive here as null. They are
		// answered identically on purpose - distinguishing them would tell an attacker which
		// account ids have codes outstanding.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.ExistingAccount();
		fixture.HasNoActiveOtp(1, AtsEmailAccountOtpPurpose.Register);

		// Act
		var result = await fixture.Service.VerifyOtpAsync(
			Submission(AtsEmailAccountManagementFixture.GeneratedOtp, AtsEmailAccountOtpPurpose.Register),
			CancellationToken.None);

		// Assert
		result.IsVerified.Should().BeFalse();
		result.RemainingAttempts.Should().Be(0);

		// Nothing was touched: an expired code must not be able to apply the change it was
		// carrying.
		fixture.UpdatedAccounts.Should().BeEmpty();
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldAdmitTheAccountToRotation_WhenTheCodeIsRight()
	{
		// Arrange
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount(status: AtsEmailAccountStatus.Pending);

		fixture.HasActiveOtp(1, AtsEmailAccountOtpPurpose.Register);

		// Act
		var result = await fixture.Service.VerifyOtpAsync(
			Submission(AtsEmailAccountManagementFixture.GeneratedOtp, AtsEmailAccountOtpPurpose.Register),
			CancellationToken.None);

		// Assert
		result.IsVerified.Should().BeTrue();

		account.VerificationStatus.Should().Be(AtsEmailAccountStatus.Verified);
		account.VerifiedAt.Should().NotBeNull();

		fixture.UpdatedAccounts.Should().ContainSingle();
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldClearStaleBreakerState_WhenCredentialsAreProvenAgain()
	{
		// Arrange: the account was cooling down because its password had been revoked. The code
		// just proved a working one, so keeping it retired would leave a healthy mailbox idle for
		// the rest of the cooldown over a fault that no longer exists.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount(status: AtsEmailAccountStatus.NeedsReverification);

		account.ConsecutiveFailureCount = 3;
		account.CoolingDownUntil = DateTime.UtcNow.AddMinutes(15);
		account.LastFailureReason = "535 Authentication failed";

		fixture.HasActiveOtp(1, AtsEmailAccountOtpPurpose.Register);

		// Act
		await fixture.Service.VerifyOtpAsync(
			Submission(AtsEmailAccountManagementFixture.GeneratedOtp, AtsEmailAccountOtpPurpose.Register),
			CancellationToken.None);

		// Assert
		account.VerificationStatus.Should().Be(AtsEmailAccountStatus.Verified);
		account.ConsecutiveFailureCount.Should().Be(0);
		account.CoolingDownUntil.Should().BeNull();
		account.LastFailureReason.Should().BeNull();
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldDropTheCachedPool_WhenAnAccountIsVerified()
	{
		// Arrange: the registry may hold sessions authenticated with the OLD password. Leaving
		// them cached means the next send silently uses credentials the operator believes they
		// replaced.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.ExistingAccount(status: AtsEmailAccountStatus.Pending);
		fixture.HasActiveOtp(1, AtsEmailAccountOtpPurpose.Register);

		// Act
		await fixture.Service.VerifyOtpAsync(
			Submission(AtsEmailAccountManagementFixture.GeneratedOtp, AtsEmailAccountOtpPurpose.Register),
			CancellationToken.None);

		// Assert
		fixture.PoolRegistry.Verify(x => x.InvalidateAsync(1), Times.Once);
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldRemoveTheAccount_WhenADeleteCodeIsConfirmed()
	{
		// Arrange: the delete happens on the server at this moment and nowhere earlier, which is
		// why an abandoned dialog leaves the account untouched.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		fixture.HasActiveOtp(1, AtsEmailAccountOtpPurpose.Delete);

		// Act
		var result = await fixture.Service.VerifyOtpAsync(
			Submission(AtsEmailAccountManagementFixture.GeneratedOtp, AtsEmailAccountOtpPurpose.Delete),
			CancellationToken.None);

		// Assert
		result.IsVerified.Should().BeTrue();
		fixture.DeletedAccounts.Should().ContainSingle(deleted => deleted == account);

		// And the row is NOT also written back as verified - the switch returns before that.
		fixture.UpdatedAccounts.Should().BeEmpty();
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldKeepTheAccount_WhenADeleteCodeIsWrong()
	{
		// Arrange
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.ExistingAccount();
		fixture.HasActiveOtp(1, AtsEmailAccountOtpPurpose.Delete);

		// Act
		var result = await fixture.Service.VerifyOtpAsync(
			Submission("000000", AtsEmailAccountOtpPurpose.Delete),
			CancellationToken.None);

		// Assert
		result.IsVerified.Should().BeFalse();
		fixture.DeletedAccounts.Should().BeEmpty();
	}

	[Fact]
	public async Task ResendOtpAsync_ShouldInvalidateOutstandingCodes_BeforeMintingANewOne()
	{
		// Arrange: without this, three requests leave three working codes, each with its own
		// attempt budget - which multiplies the guesses the cap was meant to limit.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.ExistingAccount();
		fixture.HasNoActiveOtp(1, AtsEmailAccountOtpPurpose.Register);

		// Act
		await fixture.Service.ResendOtpAsync(
			new ResendEmailAccountOtpDTO
			{
				AtsEmailAccountId = 1,
				Purpose = AtsEmailAccountOtpPurpose.Register
			},
			CancellationToken.None);

		// Assert
		fixture.Repository.Verify(
			x => x.InvalidateOtpsAsync(
				1,
				AtsEmailAccountOtpPurpose.Register,
				It.IsAny<CancellationToken>()),
			Times.Once);

		fixture.StoredOtps.Should().ContainSingle();
	}

	[Fact]
	public async Task ResendOtpAsync_ShouldKeepThePendingChange()
	{
		// Arrange: the operator is re-requesting a code for an edit they already submitted.
		// Dropping the parked JSON would verify them into a no-op - the dialog would report
		// success and nothing would have changed.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		var edit = await fixture.Service.EditAsync(
			new EditEmailAccountDTO
			{
				AtsEmailAccountId = account.AtsEmailAccountId,
				DisplayName = account.DisplayName,
				EmailAddress = account.EmailAddress,
				SmtpHost = account.SmtpHost,
				SmtpPort = account.SmtpPort,
				AppPassword = "a-new-app-password",
				Priority = account.Priority,
				DailySendLimit = account.DailySendLimit,
				IsActive = account.IsActive
			},
			CancellationToken.None);

		edit.Should().NotBeNull();

		var pendingJson = fixture.StoredOtps[^1].PendingChangesJson;

		pendingJson.Should().NotBeNull();

		fixture.HasActiveOtp(
			1,
			AtsEmailAccountOtpPurpose.Edit,
			pendingChangesJson: pendingJson);

		// Act
		await fixture.Service.ResendOtpAsync(
			new ResendEmailAccountOtpDTO
			{
				AtsEmailAccountId = 1,
				Purpose = AtsEmailAccountOtpPurpose.Edit
			},
			CancellationToken.None);

		// Assert
		fixture.StoredOtps[^1].PendingChangesJson.Should().Be(pendingJson);
	}
}
