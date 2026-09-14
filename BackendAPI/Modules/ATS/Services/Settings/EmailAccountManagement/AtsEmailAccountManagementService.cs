using System.Text.Json;

namespace ATS.Services.Settings.EmailAccountManagement;

/// <summary>
/// The registration, verification and lifecycle rules for sender accounts.
/// </summary>
/// <remarks>
/// Two invariants run through everything here, and both are why the code exists rather than a
/// plain CRUD service:
///
/// 1. An account is only ever Verified after a code sent THROUGH ITS OWN CREDENTIALS, TO ITS OWN
///    MAILBOX, came back. Both halves matter - the session proves the password, the arrival
///    proves ownership. Sending through any other account would mark this one verified on the
///    strength of a different mailbox's password.
///
/// 2. An unproven credential never reaches the account row. A pending change is parked on the
///    OTP row as JSON until the code is confirmed, so there is no window where the selector could
///    pick up a password nobody has proven.
/// </remarks>
public class AtsEmailAccountManagementService : IAtsEmailAccountManagementService
{
	private readonly IAtsEmailAccountRepository _repository;
	private readonly ISmtpAccountPoolRegistry _poolRegistry;
	private readonly IAtsEmailSender _emailSender;
	private readonly ISecretProtector _secretProtector;
	private readonly IOtpService _otpService;
	private readonly IHashService _hashService;
	private readonly AtsEmailDeliveryOptions _options;
	private readonly int _otpExpiryInMinutes;
	private readonly ILogger<AtsEmailAccountManagementService> _logger;

	public AtsEmailAccountManagementService(
		IAtsEmailAccountRepository repository,
		ISmtpAccountPoolRegistry poolRegistry,
		IAtsEmailSender emailSender,
		ISecretProtector secretProtector,
		IOtpService otpService,
		IHashService hashService,
		IOptions<AtsEmailDeliveryOptions> options,
		IConfiguration configuration,
		ILogger<AtsEmailAccountManagementService> logger)
	{
		_repository = repository;
		_poolRegistry = poolRegistry;
		_emailSender = emailSender;
		_secretProtector = secretProtector;
		_otpService = otpService;
		_hashService = hashService;
		_options = options.Value;
		_logger = logger;

		// Read as a string and parsed by hand rather than GetValue<int?>. The appsettings files
		// hold "${ATS__...}" placeholders that the deployment replaces with a real environment
		// variable; where one is missing the literal placeholder survives, and GetValue<int?>
		// would throw on it at start-up. An unset expiry should fall back to the default, not
		// take the API down.
		var configured = configuration[AtsEmailAccountOtpPolicy.ConfigurationKey];

		_otpExpiryInMinutes = int.TryParse(configured, out var minutes) && minutes > 0
			? minutes
			: AtsEmailAccountOtpPolicy.DefaultExpiryInMinutes;
	}

	public async Task<List<EmailAccountDTO>> GetAccountsAsync(CancellationToken cancellationToken)
	{
		var snapshots = await _repository.GetSnapshotsAsync(cancellationToken);

		// Once for the whole list: evaluating UtcNow per row would let a cooldown lapse
		// mid-projection and show two accounts in states that never coexisted.
		var now = DateTime.UtcNow;

		return snapshots.Select(snapshot => ToDto(snapshot, now)).ToList();
	}

	public async Task<EmailAccountOtpSentDTO> RegisterAsync(
		RegisterEmailAccountDTO account,
		CancellationToken cancellationToken)
	{
		var emailAddress = Normalize(account.EmailAddress);

		if (await _repository.EmailAddressExistsAsync(emailAddress, null, cancellationToken))
		{
			// Not merely a tidiness rule: two rows for one mailbox share a provider quota that
			// the selector counts separately, so it would believe it has twice the headroom it
			// really has and walk straight into the daily cap.
			throw new ConflictException($"{emailAddress} is already registered as a sender account.");
		}

		var priority = account.Priority ?? await _repository.GetNextAvailablePriorityAsync(cancellationToken);

		if (await _repository.PriorityExistsAsync(priority, null, cancellationToken))
		{
			throw new ConflictException(
				$"Priority {priority} is already taken. Priorities must be unique so the order accounts are tried in is never ambiguous.");
		}

		// Proven BEFORE the row is written. A failure here leaves nothing behind to clean up,
		// and the operator gets the provider's own words rather than "registration failed".
		var otpCode = _otpService.GenerateOtp();

		await SendOtpThroughCredentialsAsync(
			new SmtpAccountCredentials(
				AtsEmailAccountId: 0,
				DisplayName: account.DisplayName,
				EmailAddress: emailAddress,
				AppPassword: account.AppPassword,
				SmtpHost: account.SmtpHost,
				SmtpPort: account.SmtpPort),
			account.DisplayName,
			otpCode,
			cancellationToken);

		var entity = new AtsEmailAccount
		{
			DisplayName = account.DisplayName.Trim(),
			EmailAddress = emailAddress,
			SmtpHost = account.SmtpHost.Trim(),
			SmtpPort = account.SmtpPort,
			EncryptedPassword = _secretProtector.Protect(
				account.AppPassword,
				AtsEmailAccountSecrets.PasswordContext(emailAddress)),
			Priority = priority,
			IsActive = account.IsActive,
			DailySendLimit = account.DailySendLimit is > 0
				? account.DailySendLimit.Value
				: _options.DefaultDailySendLimit,

			// Invisible to the selector until the code comes back. This is the guarantee that a
			// half-finished registration can never send.
			VerificationStatus = AtsEmailAccountStatus.Pending,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		await _repository.AddAsync(entity, cancellationToken);

		await StoreOtpAsync(
			entity.AtsEmailAccountId,
			AtsEmailAccountOtpPurpose.Register,
			otpCode,
			pendingChangesJson: null,
			cancellationToken);

		_logger.LogInformation(
			"Registered sender account {AccountId} ({Email}) as Pending and sent a verification code.",
			entity.AtsEmailAccountId,
			emailAddress);

		return BuildOtpSent(entity.AtsEmailAccountId, AtsEmailAccountOtpPurpose.Register, emailAddress);
	}

	public async Task<EmailAccountOtpSentDTO?> EditAsync(
		EditEmailAccountDTO account,
		CancellationToken cancellationToken)
	{
		var entity = await GetEditableAccountAsync(account.AtsEmailAccountId, cancellationToken);
		var emailAddress = Normalize(account.EmailAddress);

		if (await _repository.EmailAddressExistsAsync(emailAddress, entity.AtsEmailAccountId, cancellationToken))
		{
			throw new ConflictException($"{emailAddress} is already registered as a sender account.");
		}

		if (await _repository.PriorityExistsAsync(account.Priority, entity.AtsEmailAccountId, cancellationToken))
		{
			throw new ConflictException($"Priority {account.Priority} is already taken.");
		}

		var hasNewPassword = !string.IsNullOrWhiteSpace(account.AppPassword);

		// Exactly the four fields that can invalidate the proof a previous code gave. Priority,
		// display name, daily limit and the active flag change nothing the provider cares about,
		// so making an operator re-verify for them would only train them to click through codes.
		var credentialsChanged =
			hasNewPassword
			|| !string.Equals(entity.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase)
			|| !string.Equals(entity.SmtpHost, account.SmtpHost.Trim(), StringComparison.OrdinalIgnoreCase)
			|| entity.SmtpPort != account.SmtpPort;

		if (!credentialsChanged)
		{
			entity.DisplayName = account.DisplayName.Trim();
			entity.Priority = account.Priority;
			entity.DailySendLimit = account.DailySendLimit;
			entity.IsActive = account.IsActive;

			await _repository.UpdateAsync(entity, cancellationToken);

			_logger.LogInformation(
				"Updated sender account {AccountId} ({Email}) without re-verification: no credential field changed.",
				entity.AtsEmailAccountId,
				entity.EmailAddress);

			return null;
		}

		// The password to prove: the newly typed one, or the stored one when only the host, port
		// or address moved. Unprotect can throw when the protection key has been rotated, which
		// is worth surfacing as its own message rather than as a generic failure.
		var passwordToProve = hasNewPassword
			? account.AppPassword!
			: UnprotectStoredPassword(entity);

		var otpCode = _otpService.GenerateOtp();

		await SendOtpThroughCredentialsAsync(
			new SmtpAccountCredentials(
				AtsEmailAccountId: entity.AtsEmailAccountId,
				DisplayName: account.DisplayName,
				EmailAddress: emailAddress,
				AppPassword: passwordToProve,
				SmtpHost: account.SmtpHost,
				SmtpPort: account.SmtpPort),
			account.DisplayName,
			otpCode,
			cancellationToken);

		// Parked on the OTP row, not written to the account. Writing the new password first would
		// leave an unproven credential one status change away from rotation. It is protected
		// here, so the pending JSON never holds a plaintext password either.
		var pending = new PendingEmailAccountChanges(
			DisplayName: account.DisplayName.Trim(),
			EmailAddress: emailAddress,
			SmtpHost: account.SmtpHost.Trim(),
			SmtpPort: account.SmtpPort,
			EncryptedPassword: _secretProtector.Protect(
				passwordToProve,
				AtsEmailAccountSecrets.PasswordContext(emailAddress)),
			Priority: account.Priority,
			DailySendLimit: account.DailySendLimit,
			IsActive: account.IsActive);

		await StoreOtpAsync(
			entity.AtsEmailAccountId,
			AtsEmailAccountOtpPurpose.Edit,
			otpCode,
			JsonSerializer.Serialize(pending),
			cancellationToken);

		return BuildOtpSent(entity.AtsEmailAccountId, AtsEmailAccountOtpPurpose.Edit, emailAddress);
	}

	public async Task<EmailAccountOtpSentDTO> DeleteAsync(
		DeleteEmailAccountDTO request,
		CancellationToken cancellationToken)
	{
		var entity = await GetEditableAccountAsync(request.AtsEmailAccountId, cancellationToken);

		var otpCode = _otpService.GenerateOtp();

		await SendOtpThroughCredentialsAsync(
			new SmtpAccountCredentials(
				AtsEmailAccountId: entity.AtsEmailAccountId,
				DisplayName: entity.DisplayName,
				EmailAddress: entity.EmailAddress,
				AppPassword: UnprotectStoredPassword(entity),
				SmtpHost: entity.SmtpHost,
				SmtpPort: entity.SmtpPort),
			entity.DisplayName,
			otpCode,
			cancellationToken);

		await StoreOtpAsync(
			entity.AtsEmailAccountId,
			AtsEmailAccountOtpPurpose.Delete,
			otpCode,
			pendingChangesJson: null,
			cancellationToken);

		return BuildOtpSent(entity.AtsEmailAccountId, AtsEmailAccountOtpPurpose.Delete, entity.EmailAddress);
	}

	public async Task<EmailAccountOtpResultDTO> VerifyOtpAsync(
		VerifyEmailAccountOtpDTO request,
		CancellationToken cancellationToken)
	{
		var otp = await _repository.GetActiveOtpAsync(
			request.AtsEmailAccountId,
			request.Purpose,
			DateTime.UtcNow,
			cancellationToken);

		if (otp is null)
		{
			// Covers expired, already consumed and never issued alike. Distinguishing them would
			// tell an attacker which account ids have codes outstanding.
			return new EmailAccountOtpResultDTO
			{
				IsVerified = false,
				RemainingAttempts = 0,
				Message = "That code has expired or has already been used. Request a new one."
			};
		}

		if (!_hashService.Verify(_hashService.Hash(request.OtpCode), otp.OtpCodeHash))
		{
			otp.AttemptCount++;

			// Consumed rather than the account locked: the account is not trusted yet, so there is
			// nothing to lock, and a resend costs one email.
			var exhausted = otp.AttemptCount >= AtsEmailAccountOtpPolicy.MaxAttempts;

			if (exhausted)
			{
				otp.IsUsed = true;
			}

			await _repository.UpdateOtpAsync(otp, cancellationToken);

			return new EmailAccountOtpResultDTO
			{
				IsVerified = false,
				RemainingAttempts = Math.Max(0, AtsEmailAccountOtpPolicy.MaxAttempts - otp.AttemptCount),
				Message = exhausted
					? "Too many incorrect codes. Request a new one."
					: "That code is not correct."
			};
		}

		otp.IsUsed = true;
		otp.VerifiedAt = DateTime.UtcNow;

		await _repository.UpdateOtpAsync(otp, cancellationToken);

		await ApplyVerifiedChangeAsync(otp, cancellationToken);

		return new EmailAccountOtpResultDTO
		{
			IsVerified = true,
			RemainingAttempts = AtsEmailAccountOtpPolicy.MaxAttempts,
			Message = request.Purpose switch
			{
				AtsEmailAccountOtpPurpose.Delete => "The sender account has been removed.",
				AtsEmailAccountOtpPurpose.Edit => "The changes have been saved and the account is back in rotation.",
				_ => "The sender account is verified and will now be used to send invitations."
			}
		};
	}

	public async Task<EmailAccountOtpSentDTO> ResendOtpAsync(
		ResendEmailAccountOtpDTO request,
		CancellationToken cancellationToken)
	{
		var entity = await _repository.GetAccountAsync(request.AtsEmailAccountId, cancellationToken)
			?? throw new NotFoundException($"Sender account {request.AtsEmailAccountId} was not found.");

		// The pending change, if any, has to survive the resend: the operator is re-requesting a
		// code for an edit they already submitted, and dropping the parked JSON would verify them
		// into a no-op.
		var existing = await _repository.GetActiveOtpAsync(
			request.AtsEmailAccountId,
			request.Purpose,
			DateTime.UtcNow,
			cancellationToken);

		var pendingChangesJson = existing?.PendingChangesJson;

		var password = pendingChangesJson is null
			? UnprotectStoredPassword(entity)
			: UnprotectPendingPassword(pendingChangesJson);

		var target = pendingChangesJson is null
			? entity.EmailAddress
			: DeserializePending(pendingChangesJson).EmailAddress;

		var otpCode = _otpService.GenerateOtp();

		await SendOtpThroughCredentialsAsync(
			new SmtpAccountCredentials(
				AtsEmailAccountId: entity.AtsEmailAccountId,
				DisplayName: entity.DisplayName,
				EmailAddress: target,
				AppPassword: password,
				SmtpHost: entity.SmtpHost,
				SmtpPort: entity.SmtpPort),
			entity.DisplayName,
			otpCode,
			cancellationToken);

		await StoreOtpAsync(
			entity.AtsEmailAccountId,
			request.Purpose,
			otpCode,
			pendingChangesJson,
			cancellationToken);

		return BuildOtpSent(entity.AtsEmailAccountId, request.Purpose, target);
	}

	#region Internals
	/// <summary>
	/// Loads an account and refuses while a send is in flight through it.
	/// </summary>
	/// <remarks>
	/// The lease window is a few seconds. Swapping credentials underneath an in-flight send
	/// either fails it or, worse, sends it from the wrong mailbox.
	/// </remarks>
	private async Task<AtsEmailAccount> GetEditableAccountAsync(
		int accountId,
		CancellationToken cancellationToken)
	{
		var entity = await _repository.GetAccountAsync(accountId, cancellationToken)
			?? throw new NotFoundException($"Sender account {accountId} was not found.");

		if (_poolRegistry.IsLeased(accountId))
		{
			throw new ConflictException(
				$"{entity.EmailAddress} is sending right now. Try again in a few seconds.");
		}

		return entity;
	}

	/// <summary>
	/// Sends the code through the credentials being proven, and turns a refusal into a message
	/// the operator can act on.
	/// </summary>
	private async Task SendOtpThroughCredentialsAsync(
		SmtpAccountCredentials credentials,
		string displayName,
		string otpCode,
		CancellationToken cancellationToken)
	{
		var result = await _emailSender.SendWithCredentialsAsync(
			credentials,
			credentials.EmailAddress,
			"Verify your ATS sender email",
			ATSEmailService.AtsEmailAccountOtpBody(displayName, otpCode, _otpExpiryInMinutes),
			cancellationToken);

		if (result.IsSent)
		{
			return;
		}

		_logger.LogWarning(
			"Could not send a verification code to {Email}: {StatusCode} {Message}",
			credentials.EmailAddress,
			result.StatusCode,
			result.Message);

		// The provider's own words, because "authentication failed - check the app password" and
		// "we could not reach smtp.gmail.com" need completely different responses, and the
		// operator is standing at the form right now.
		throw new BadRequestException(
			"The email account could not be verified.",
			string.IsNullOrWhiteSpace(result.Message)
				? "The mail server refused the connection. Check the address, host and port."
				: result.Message);
	}

	private async Task StoreOtpAsync(
		int accountId,
		string purpose,
		string otpCode,
		string? pendingChangesJson,
		CancellationToken cancellationToken)
	{
		// Outstanding codes go first. Without this a resend leaves the previous code valid, so
		// three requests would mean three working codes each with its own attempt budget.
		await _repository.InvalidateOtpsAsync(accountId, purpose, cancellationToken);

		await _repository.AddOtpAsync(
			new AtsEmailAccountOtp
			{
				AtsEmailAccountId = accountId,
				Purpose = purpose,

				// Hashed: a code readable in the database is not a second factor.
				OtpCodeHash = _hashService.Hash(otpCode),
				PendingChangesJson = pendingChangesJson,
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryInMinutes)
			},
			cancellationToken);
	}

	/// <summary>Applies whatever the confirmed code was approving.</summary>
	private async Task ApplyVerifiedChangeAsync(
		AtsEmailAccountOtp otp,
		CancellationToken cancellationToken)
	{
		var entity = await _repository.GetAccountAsync(otp.AtsEmailAccountId, cancellationToken);

		if (entity is null)
		{
			return;
		}

		switch (otp.Purpose)
		{
			case AtsEmailAccountOtpPurpose.Delete:
				await _repository.DeleteAsync(entity, cancellationToken);

				// Drops the pool and limiter with it, closing the account's SMTP sessions rather
				// than leaving them open against a mailbox nothing will select again.
				await _poolRegistry.InvalidateAsync(entity.AtsEmailAccountId);

				_logger.LogWarning(
					"Sender account {AccountId} ({Email}) was deleted. Its volume now falls to the remaining accounts.",
					entity.AtsEmailAccountId,
					entity.EmailAddress);
				return;

			case AtsEmailAccountOtpPurpose.Edit:
				if (otp.PendingChangesJson is not null)
				{
					var pending = DeserializePending(otp.PendingChangesJson);

					entity.DisplayName = pending.DisplayName;
					entity.EmailAddress = pending.EmailAddress;
					entity.SmtpHost = pending.SmtpHost;
					entity.SmtpPort = pending.SmtpPort;
					entity.EncryptedPassword = pending.EncryptedPassword;
					entity.Priority = pending.Priority;
					entity.DailySendLimit = pending.DailySendLimit;
					entity.IsActive = pending.IsActive;
				}

				break;
		}

		// Register and Edit both land here. The code just proved the credentials work, so the
		// breaker state from before the change is stale - an account that was cooling down
		// because of a revoked password should come back immediately once a new one is proven.
		entity.VerificationStatus = AtsEmailAccountStatus.Verified;
		entity.VerifiedAt = DateTime.UtcNow;
		entity.ConsecutiveFailureCount = 0;
		entity.CoolingDownUntil = null;
		entity.LastFailureReason = null;

		await _repository.UpdateAsync(entity, cancellationToken);

		// The registry may hold a context built from the old credentials.
		await _poolRegistry.InvalidateAsync(entity.AtsEmailAccountId);

		_logger.LogInformation(
			"Sender account {AccountId} ({Email}) is verified and in rotation.",
			entity.AtsEmailAccountId,
			entity.EmailAddress);
	}

	private string UnprotectStoredPassword(AtsEmailAccount entity)
	{
		try
		{
			return _secretProtector.Unprotect(
				entity.EncryptedPassword,
				AtsEmailAccountSecrets.PasswordContext(entity.EmailAddress));
		}
		catch (System.Security.Cryptography.CryptographicException)
		{
			// Almost always a rotated Security:SecretProtectionKey. Saying so is the difference
			// between re-entering one password and hunting a decryption error.
			throw new BadRequestException(
				"The stored password for this account can no longer be read.",
				"This usually means the secret protection key was rotated. Re-enter the app password to continue.");
		}
	}

	private string UnprotectPendingPassword(string pendingChangesJson)
	{
		var pending = DeserializePending(pendingChangesJson);

		try
		{
			return _secretProtector.Unprotect(
				pending.EncryptedPassword,
				AtsEmailAccountSecrets.PasswordContext(pending.EmailAddress));
		}
		catch (System.Security.Cryptography.CryptographicException)
		{
			throw new BadRequestException(
				"The pending password for this account can no longer be read.",
				"Start the edit again and re-enter the app password.");
		}
	}

	private static PendingEmailAccountChanges DeserializePending(string json) =>
		JsonSerializer.Deserialize<PendingEmailAccountChanges>(json)
		?? throw new BadRequestException(
			"The pending change for this account could not be read.",
			"Start the edit again.");

	private static string Normalize(string emailAddress) =>
		emailAddress.Trim().ToLowerInvariant();

	private EmailAccountOtpSentDTO BuildOtpSent(int accountId, string purpose, string emailAddress) =>
		new()
		{
			AtsEmailAccountId = accountId,
			Purpose = purpose,
			EmailAddress = emailAddress,
			ExpiresAt = DateTime.UtcNow.AddMinutes(_otpExpiryInMinutes),
			ExpiryInMinutes = _otpExpiryInMinutes
		};

	private EmailAccountDTO ToDto(AtsEmailAccountSnapshot snapshot, DateTime now) =>
		new()
		{
			AtsEmailAccountId = snapshot.AtsEmailAccountId,
			DisplayName = snapshot.DisplayName,
			EmailAddress = snapshot.EmailAddress,
			SmtpHost = snapshot.SmtpHost,
			SmtpPort = snapshot.SmtpPort,
			Priority = snapshot.Priority,
			IsActive = snapshot.IsActive,
			DailySendLimit = snapshot.DailySendLimit,
			VerificationStatus = snapshot.VerificationStatus,
			VerifiedAt = snapshot.VerifiedAt,
			ConsecutiveFailureCount = snapshot.ConsecutiveFailureCount,
			CoolingDownUntil = snapshot.CoolingDownUntil,
			LastFailureReason = snapshot.LastFailureReason,
			LastSentAt = snapshot.LastSentAt,
			ConsumedInWindow = snapshot.ConsumedInWindow,
			RemainingInWindow = snapshot.RemainingInWindow,
			IsInUse = _poolRegistry.IsLeased(snapshot.AtsEmailAccountId),

			// The selector's own rule, not the table's reading of it.
			IsSendable = snapshot.IsSendable(now),
			HasPassword = true
		};

	/// <summary>
	/// An edit waiting on its code. Serialized onto the OTP row so an unproven credential never
	/// touches the account.
	/// </summary>
	private sealed record PendingEmailAccountChanges(
		string DisplayName,
		string EmailAddress,
		string SmtpHost,
		int SmtpPort,
		string EncryptedPassword,
		int Priority,
		int DailySendLimit,
		bool IsActive);
	#endregion
}
