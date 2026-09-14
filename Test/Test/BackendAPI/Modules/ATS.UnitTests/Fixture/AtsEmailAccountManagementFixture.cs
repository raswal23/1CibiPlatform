using ATS.Configuration;
using ATS.Constants;
using ATS.Data.Entities;
using ATS.Data.Repository.EmailAccounts;
using ATS.Services.EmailAccounts;
using ATS.Services.EmailService;
using ATS.Services.Settings.EmailAccountManagement;
using BuildingBlocks.SharedServices.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

/// <summary>
/// The management service over mocked persistence, a mocked sender and reversible fakes for the
/// protector, hasher and code generator.
/// </summary>
/// <remarks>
/// The protector and hasher are faked rather than mocked so that a round trip actually works: the
/// service protects a password on the way in and unprotects it on a later resend, and a mock
/// returning a constant would make those two look correct while hiding a context mismatch.
///
/// The sender defaults to accepting everything. A test about registration conflicts should not
/// have to think about SMTP, and a test about SMTP refusals says so explicitly.
/// </remarks>
public sealed class AtsEmailAccountManagementFixture
{
	public Mock<IAtsEmailAccountRepository> Repository { get; } = new();

	public Mock<ISmtpAccountPoolRegistry> PoolRegistry { get; } = new();

	public Mock<IAtsEmailSender> EmailSender { get; } = new();

	public AtsEmailAccountManagementService Service { get; }

	/// <summary>The code the fake generator hands out, and therefore the correct answer.</summary>
	public const string GeneratedOtp = "123456";

	/// <summary>Codes stored by the service, newest last. Keyed nowhere - tests read the last.</summary>
	public List<AtsEmailAccountOtp> StoredOtps { get; } = [];

	/// <summary>The account rows written back by the service.</summary>
	public List<AtsEmailAccount> UpdatedAccounts { get; } = [];

	/// <summary>Accounts the service removed.</summary>
	public List<AtsEmailAccount> DeletedAccounts { get; } = [];

	public AtsEmailAccountManagementFixture()
	{
		EmailSender
			.Setup(x => x.SendWithCredentialsAsync(
				It.IsAny<SmtpAccountCredentials>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(EmailDeliveryResult.Sent);

		Repository
			.Setup(x => x.AddOtpAsync(It.IsAny<AtsEmailAccountOtp>(), It.IsAny<CancellationToken>()))
			.Callback<AtsEmailAccountOtp, CancellationToken>((otp, _) => StoredOtps.Add(otp))
			.Returns(Task.CompletedTask);

		Repository
			.Setup(x => x.UpdateAsync(It.IsAny<AtsEmailAccount>(), It.IsAny<CancellationToken>()))
			.Callback<AtsEmailAccount, CancellationToken>((account, _) => UpdatedAccounts.Add(account))
			.Returns(Task.CompletedTask);

		Repository
			.Setup(x => x.DeleteAsync(It.IsAny<AtsEmailAccount>(), It.IsAny<CancellationToken>()))
			.Callback<AtsEmailAccount, CancellationToken>((account, _) => DeletedAccounts.Add(account))
			.Returns(Task.CompletedTask);

		var otpService = new Mock<IOtpService>();

		otpService.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns(GeneratedOtp);

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ AtsEmailAccountOtpPolicy.ConfigurationKey, "10" }
			})
			.Build();

		Service = new AtsEmailAccountManagementService(
			Repository.Object,
			PoolRegistry.Object,
			EmailSender.Object,
			new TestProtector(),
			otpService.Object,
			new PrefixHasher(),
			Options.Create(new AtsEmailDeliveryOptions()),
			configuration,
			NullLogger<AtsEmailAccountManagementService>.Instance);
	}

	/// <summary>A stored, verified account the service can load and edit.</summary>
	public AtsEmailAccount ExistingAccount(
		int id = 1,
		string emailAddress = "sender1@example.com",
		string smtpHost = "smtp.example.com",
		int smtpPort = 587,
		string status = AtsEmailAccountStatus.Verified)
	{
		var account = new AtsEmailAccount
		{
			AtsEmailAccountId = id,
			DisplayName = $"Sender {id}",
			EmailAddress = emailAddress,
			SmtpHost = smtpHost,
			SmtpPort = smtpPort,
			EncryptedPassword = new TestProtector().Protect(
				"stored-app-password",
				AtsEmailAccountSecrets.PasswordContext(emailAddress)),
			Priority = id,
			IsActive = true,
			DailySendLimit = 450,
			VerificationStatus = status,
			VerifiedAt = DateTime.UtcNow.AddDays(-1),
			CreatedAt = DateTime.UtcNow.AddDays(-1),
			UpdatedAt = DateTime.UtcNow.AddDays(-1)
		};

		Repository
			.Setup(x => x.GetAccountAsync(id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(account);

		return account;
	}

	/// <summary>Makes the given code the one outstanding for an account and purpose.</summary>
	public AtsEmailAccountOtp HasActiveOtp(
		int accountId,
		string purpose,
		string code = GeneratedOtp,
		int attemptCount = 0,
		string? pendingChangesJson = null)
	{
		var otp = new AtsEmailAccountOtp
		{
			AtsEmailAccountOtpId = 1,
			AtsEmailAccountId = accountId,
			Purpose = purpose,
			OtpCodeHash = new PrefixHasher().Hash(code),
			AttemptCount = attemptCount,
			PendingChangesJson = pendingChangesJson,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddMinutes(10)
		};

		Repository
			.Setup(x => x.GetActiveOtpAsync(
				accountId,
				purpose,
				It.IsAny<DateTime>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(otp);

		return otp;
	}

	/// <summary>Makes the account hold NO usable code - expired, consumed or never issued.</summary>
	public void HasNoActiveOtp(int accountId, string purpose) =>
		Repository
			.Setup(x => x.GetActiveOtpAsync(
				accountId,
				purpose,
				It.IsAny<DateTime>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((AtsEmailAccountOtp?)null);

	/// <summary>
	/// Reversible and deterministic, so protect-then-unprotect round trips in a test.
	/// </summary>
	/// <remarks>
	/// The context is part of the stored value on purpose: a ciphertext bound to one mailbox and
	/// read back under another must fail, because that is exactly what the real AES-GCM protector
	/// does when a row is lifted between accounts.
	/// </remarks>
	public sealed class TestProtector : ISecretProtector
	{
		private const string Separator = "||";

		public string Protect(string plaintext, string context) =>
			$"{context}{Separator}{plaintext}";

		public string Unprotect(string protectedValue, string context)
		{
			var index = protectedValue.IndexOf(Separator, StringComparison.Ordinal);

			if (index < 0 || protectedValue[..index] != context)
			{
				throw new System.Security.Cryptography.CryptographicException(
					"The protected value does not belong to this context.");
			}

			return protectedValue[(index + Separator.Length)..];
		}
	}

	/// <summary>A hasher that is not one, so a test can assert on which code was stored.</summary>
	private sealed class PrefixHasher : IHashService
	{
		public string Hash(string input) => $"hashed:{input}";

		public bool Verify(string input, string hash) => hash == $"hashed:{input}";
	}
}
