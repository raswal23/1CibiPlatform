using ATS.Constants;
using ATS.Data.Entities;
using ATS.DTO;
using ATS.Services.EmailAccounts;
using ATS.Services.EmailService;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// Registration, editing and deletion of sender accounts.
/// </summary>
/// <remarks>
/// The rules worth defending here are the ones that are invisible when they break. An edit that
/// re-verifies too eagerly trains operators to click through codes; one that re-verifies too
/// rarely puts an unproven password into rotation. And an unproven credential must never reach the
/// account row at all - it is parked on the code until somebody confirms it.
/// </remarks>
public class AtsEmailAccountManagementServiceTests
{
	private static EditEmailAccountDTO EditOf(
		AtsEmailAccount account,
		string? displayName = null,
		string? emailAddress = null,
		string? smtpHost = null,
		int? smtpPort = null,
		string? appPassword = null,
		int? priority = null,
		int? dailySendLimit = null,
		bool? isActive = null) => new()
	{
		AtsEmailAccountId = account.AtsEmailAccountId,
		DisplayName = displayName ?? account.DisplayName,
		EmailAddress = emailAddress ?? account.EmailAddress,
		SmtpHost = smtpHost ?? account.SmtpHost,
		SmtpPort = smtpPort ?? account.SmtpPort,
		AppPassword = appPassword,
		Priority = priority ?? account.Priority,
		DailySendLimit = dailySendLimit ?? account.DailySendLimit,
		IsActive = isActive ?? account.IsActive
	};

	#region Registration
	[Fact]
	public async Task RegisterAsync_ShouldProveTheCredentials_BeforeWritingTheRow()
	{
		// Arrange: the provider refuses the app password. Nothing must be left behind to clean up,
		// and the operator gets the provider's own words while they are still at the form.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.EmailSender
			.Setup(x => x.SendWithCredentialsAsync(
				It.IsAny<SmtpAccountCredentials>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(EmailDeliveryResult.Permanent(
				"535",
				"535 5.7.8 Username and Password not accepted",
				EmailFailureScope.Account));

		// Act
		Func<Task> act = async () => await fixture.Service.RegisterAsync(
			new RegisterEmailAccountDTO
			{
				DisplayName = "Ops",
				EmailAddress = "ops@example.com",
				SmtpHost = "smtp.example.com",
				SmtpPort = 587,
				AppPassword = "wrong-password"
			},
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<BadRequestException>();

		fixture.Repository.Verify(
			x => x.AddAsync(It.IsAny<AtsEmailAccount>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task RegisterAsync_ShouldWriteThePendingRow_WhenTheCodeGoesOut()
	{
		// Arrange
		var fixture = new AtsEmailAccountManagementFixture();

		AtsEmailAccount? written = null;

		fixture.Repository
			.Setup(x => x.AddAsync(
				It.IsAny<AtsEmailAccount>(),
				It.IsAny<CancellationToken>()))
			.Callback<AtsEmailAccount, CancellationToken>(
				(account, _) => written = account)
			.Returns(Task.CompletedTask);

		// Act
		var sent = await fixture.Service.RegisterAsync(
			new RegisterEmailAccountDTO
			{
				DisplayName = "Ops",
				EmailAddress = "  OPS@Example.com ",
				SmtpHost = "smtp.example.com",
				SmtpPort = 587,
				AppPassword = "app-password",
				Priority = 1
			},
			CancellationToken.None);

		// Assert
		written.Should().NotBeNull();

		// Pending, not Verified. This is what keeps a half-finished registration out of the queue.
		written!.VerificationStatus.Should().Be(AtsEmailAccountStatus.Pending);

		// Normalised, because the protection context is built from the address and SMTP mailboxes
		// are case-insensitive - a row saved as "OPS@" would not decrypt when read back as "ops@".
		written.EmailAddress.Should().Be("ops@example.com");

		// The stored value is not the password.
		written.EncryptedPassword.Should().NotBe("app-password");

		sent.Purpose.Should().Be(AtsEmailAccountOtpPurpose.Register);
		sent.EmailAddress.Should().Be("ops@example.com");
	}

	[Fact]
	public async Task RegisterAsync_ShouldRefuseADuplicateMailbox()
	{
		// Arrange: two rows for one mailbox share a provider quota the selector counts separately,
		// so it would believe it has twice the headroom it really has.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.Repository
			.Setup(x => x.EmailAddressExistsAsync(
				"ops@example.com",
				null,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		Func<Task> act = async () => await fixture.Service.RegisterAsync(
			new RegisterEmailAccountDTO
			{
				DisplayName = "Ops",
				EmailAddress = "ops@example.com",
				AppPassword = "app-password"
			},
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<ConflictException>();

		// And no code was sent - the refusal comes before anything touches SMTP.
		fixture.EmailSender.Verify(
			x => x.SendWithCredentialsAsync(
				It.IsAny<SmtpAccountCredentials>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task RegisterAsync_ShouldRefuseADuplicatePriority()
	{
		// Arrange: priorities are unique so the order accounts are tried in is never ambiguous. A
		// tie would make failover depend on row order, which is not a rule anyone can reason about.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.Repository
			.Setup(x => x.PriorityExistsAsync(1, null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		Func<Task> act = async () => await fixture.Service.RegisterAsync(
			new RegisterEmailAccountDTO
			{
				DisplayName = "Ops",
				EmailAddress = "ops@example.com",
				AppPassword = "app-password",
				Priority = 1
			},
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<ConflictException>();
	}
	#endregion

	#region Edit
	[Fact]
	public async Task EditAsync_ShouldSaveImmediately_WhenNoCredentialFieldChanged()
	{
		// Arrange: priority, display name, daily limit and the active flag change nothing the
		// provider cares about. Demanding a code for them would only teach operators to click
		// through codes without reading them.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		// Act
		var result = await fixture.Service.EditAsync(
			EditOf(account, displayName: "Renamed", priority: 4, dailySendLimit: 300, isActive: false),
			CancellationToken.None);

		// Assert
		result.Should().BeNull();

		account.DisplayName.Should().Be("Renamed");
		account.Priority.Should().Be(4);
		account.DailySendLimit.Should().Be(300);
		account.IsActive.Should().BeFalse();

		// Still verified: nothing happened that could invalidate the proof the last code gave.
		account.VerificationStatus.Should().Be(AtsEmailAccountStatus.Verified);

		fixture.StoredOtps.Should().BeEmpty();
	}

	[Theory]
	[InlineData("moved@example.com", null, null, null)]
	[InlineData(null, "smtp.office365.com", null, null)]
	[InlineData(null, null, 465, null)]
	[InlineData(null, null, null, "a-new-app-password")]
	public async Task EditAsync_ShouldRequireACode_WhenACredentialFieldChanged(
		string? emailAddress,
		string? smtpHost,
		int? smtpPort,
		string? appPassword)
	{
		// Arrange: exactly the four fields that can invalidate the proof a previous code gave.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		// Act
		var result = await fixture.Service.EditAsync(
			EditOf(
				account,
				emailAddress: emailAddress,
				smtpHost: smtpHost,
				smtpPort: smtpPort,
				appPassword: appPassword),
			CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Purpose.Should().Be(AtsEmailAccountOtpPurpose.Edit);

		fixture.StoredOtps.Should().ContainSingle();
	}

	[Fact]
	public async Task EditAsync_ShouldParkTheChange_RatherThanWritingItToTheAccount()
	{
		// Arrange: THE invariant. Writing the new password to the account first would leave an
		// unproven credential one status change away from rotation.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		var originalPassword = account.EncryptedPassword;

		// Act
		await fixture.Service.EditAsync(
			EditOf(account, appPassword: "a-new-app-password", displayName: "Renamed"),
			CancellationToken.None);

		// Assert: the row is untouched - not the password, and not even the display name that
		// travelled alongside it.
		account.EncryptedPassword.Should().Be(originalPassword);
		account.DisplayName.Should().NotBe("Renamed");

		fixture.UpdatedAccounts.Should().BeEmpty();

		// It is on the code instead, and it went through the protector on the way - the JSON holds
		// an EncryptedPassword, never the raw AppPassword field the operator typed.
		//
		// This asserts the protector was applied, not that the result is unreadable: the fake
		// protector here is reversible by design, so the plaintext is still visible inside the
		// protected value. In production that value is AES-GCM ciphertext.
		var pending = fixture.StoredOtps.Should().ContainSingle().Subject.PendingChangesJson;

		pending.Should().NotBeNull();
		pending.Should().NotContain("AppPassword");
		pending.Should().Contain(
			new AtsEmailAccountManagementFixture.TestProtector().Protect(
				"a-new-app-password",
				AtsEmailAccountSecrets.PasswordContext(account.EmailAddress)));
	}

	[Fact]
	public async Task EditAsync_ShouldApplyTheParkedChange_OnceTheCodeIsConfirmed()
	{
		// Arrange
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		await fixture.Service.EditAsync(
			EditOf(account, appPassword: "a-new-app-password", displayName: "Renamed", priority: 7),
			CancellationToken.None);

		fixture.HasActiveOtp(
			account.AtsEmailAccountId,
			AtsEmailAccountOtpPurpose.Edit,
			pendingChangesJson: fixture.StoredOtps[^1].PendingChangesJson);

		// Act
		var result = await fixture.Service.VerifyOtpAsync(
			new VerifyEmailAccountOtpDTO
			{
				AtsEmailAccountId = account.AtsEmailAccountId,
				Purpose = AtsEmailAccountOtpPurpose.Edit,
				OtpCode = AtsEmailAccountManagementFixture.GeneratedOtp
			},
			CancellationToken.None);

		// Assert
		result.IsVerified.Should().BeTrue();

		account.DisplayName.Should().Be("Renamed");
		account.Priority.Should().Be(7);
		account.VerificationStatus.Should().Be(AtsEmailAccountStatus.Verified);

		// The new password landed, and it landed protected.
		account.EncryptedPassword.Should().NotBe("a-new-app-password");
		account.EncryptedPassword.Should().Contain("a-new-app-password");
	}

	[Fact]
	public async Task EditAsync_ShouldSurfaceTheProviderRefusal_WhenTheNewPasswordIsWrong()
	{
		// Arrange: the same value registration gets - the operator finds out now rather than when
		// the queue stalls.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		fixture.EmailSender
			.Setup(x => x.SendWithCredentialsAsync(
				It.IsAny<SmtpAccountCredentials>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(EmailDeliveryResult.Permanent(
				"535",
				"535 5.7.8 Username and Password not accepted",
				EmailFailureScope.Account));

		// Act
		Func<Task> act = async () => await fixture.Service.EditAsync(
			EditOf(account, appPassword: "still-wrong"),
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<BadRequestException>();

		fixture.StoredOtps.Should().BeEmpty();
	}
	#endregion

	#region In-use guard
	[Fact]
	public async Task EditAsync_ShouldRefuse_WhileASendIsInFlight()
	{
		// Arrange: swapping credentials underneath an open session either fails the send or, worse,
		// sends it from the wrong mailbox.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		fixture.PoolRegistry.Setup(x => x.IsLeased(account.AtsEmailAccountId)).Returns(true);

		// Act
		Func<Task> act = async () => await fixture.Service.EditAsync(
			EditOf(account, displayName: "Renamed"),
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<ConflictException>();
	}

	[Fact]
	public async Task DeleteAsync_ShouldRefuse_WhileASendIsInFlight()
	{
		// Arrange
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		fixture.PoolRegistry.Setup(x => x.IsLeased(account.AtsEmailAccountId)).Returns(true);

		// Act
		Func<Task> act = async () => await fixture.Service.DeleteAsync(
			new DeleteEmailAccountDTO { AtsEmailAccountId = account.AtsEmailAccountId },
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<ConflictException>();
	}

	[Fact]
	public async Task EditAsync_ShouldThrowNotFound_WhenTheAccountIsGone()
	{
		// Arrange
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.Repository
			.Setup(x => x.GetAccountAsync(99, It.IsAny<CancellationToken>()))
			.ReturnsAsync((AtsEmailAccount?)null);

		// Act
		Func<Task> act = async () => await fixture.Service.EditAsync(
			new EditEmailAccountDTO
			{
				AtsEmailAccountId = 99,
				DisplayName = "Ghost",
				EmailAddress = "ghost@example.com",
				SmtpHost = "smtp.example.com",
				SmtpPort = 587,
				Priority = 1,
				DailySendLimit = 450,
				IsActive = true
			},
			CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();
	}
	#endregion

	#region Delete
	[Fact]
	public async Task DeleteAsync_ShouldOnlySendACode_AndRemoveNothing()
	{
		// Arrange: deletion is as consequential as registration - the remaining accounts absorb
		// the volume, and if it was the last one the queue stops. So the removal waits for the code.
		var fixture = new AtsEmailAccountManagementFixture();

		var account = fixture.ExistingAccount();

		// Act
		var sent = await fixture.Service.DeleteAsync(
			new DeleteEmailAccountDTO { AtsEmailAccountId = account.AtsEmailAccountId },
			CancellationToken.None);

		// Assert
		sent.Purpose.Should().Be(AtsEmailAccountOtpPurpose.Delete);
		sent.EmailAddress.Should().Be(account.EmailAddress);

		fixture.DeletedAccounts.Should().BeEmpty();
	}
	#endregion

	#region Read
	[Fact]
	public async Task GetAccountsAsync_ShouldNeverReturnACredential()
	{
		// Arrange: the read DTO is projected from a snapshot that has no password field at all, so
		// this is a property of the type rather than a rule someone has to remember. The assertion
		// exists because a future field added to the DTO could quietly break it.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.Repository
			.Setup(x => x.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([AtsEmailAccountFixture.Account(id: 1, priority: 1, consumedInWindow: 120)]);

		// Act
		var accounts = await fixture.Service.GetAccountsAsync(CancellationToken.None);

		// Assert
		var dto = accounts.Should().ContainSingle().Subject;

		typeof(EmailAccountDTO)
			.GetProperties()
			.Should()
			.NotContain(property =>
				property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
				&& property.PropertyType == typeof(string));

		dto.ConsumedInWindow.Should().Be(120);
		dto.RemainingInWindow.Should().Be(330);
		dto.IsSendable.Should().BeTrue();
	}

	[Fact]
	public async Task GetAccountsAsync_ShouldFlagAnAccountThatIsSendingRightNow()
	{
		// Arrange: the badge that explains why edit and delete are refused.
		var fixture = new AtsEmailAccountManagementFixture();

		fixture.Repository
			.Setup(x => x.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([AtsEmailAccountFixture.Account(id: 1, priority: 1)]);

		fixture.PoolRegistry.Setup(x => x.IsLeased(1)).Returns(true);

		// Act
		var accounts = await fixture.Service.GetAccountsAsync(CancellationToken.None);

		// Assert
		accounts.Should().ContainSingle().Which.IsInUse.Should().BeTrue();
	}
	#endregion
}
