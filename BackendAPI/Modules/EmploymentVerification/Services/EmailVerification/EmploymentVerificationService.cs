namespace EmploymentVerification.Services;

public sealed class EmploymentVerificationService : IEmploymentVerificationService
{
	private readonly IEmploymentVerificationRepository _repository;
	private readonly IATSVerificationDataProvider _atsProvider;
	private readonly IEmailService _emailService;
	private readonly IHashService _hashService;
	private readonly IConfiguration _configuration;
	private readonly string _applicationformBaseUrl;
	private readonly int _tokenExpiryHours;

	public EmploymentVerificationService(
		IEmploymentVerificationRepository repository,
		IATSVerificationDataProvider atsProvider,
		[FromKeyedServices("ats")] IEmailService emailService,
		IHashService hashService,
		IConfiguration configuration)
	{
		_repository = repository;
		_atsProvider = atsProvider;
		_emailService = emailService;
		_hashService = hashService;
		_configuration = configuration;
		_applicationformBaseUrl = _configuration.GetSection("EmailVerification").GetValue<string>("EmploymentVerificationUrl") ?? string.Empty;
		_tokenExpiryHours = _configuration.GetSection("EmailVerification").GetValue<int>("TokenExpiryInHours", 72);
	}

	public async Task<IReadOnlyList<EmploymentVerificationRequest>> ListAsync(CancellationToken cancellationToken) =>
		await _repository.ListAsync(cancellationToken);

	/// <summary>
	/// Lists the in-progress ATS candidates that still need a verification email.
	/// A candidate is withheld while a request is awaiting a response or has been
	/// confirmed; rejected and lapsed requests release the candidate so a fresh
	/// request can be sent.
	/// </summary>
	public async Task<IReadOnlyList<ATSInProgressEmploymentRecord>> GetAvailableATSRecordsAsync(
		CancellationToken cancellationToken)
	{
		var atsRecords = await _atsProvider.GetInProgressEmploymentAsync(cancellationToken);

		if (atsRecords.Count == 0)
		{
			return atsRecords;
		}

		var blockedSubjectIds = await _repository.ListBlockedAtsSubjectIdsAsync(
			DateTime.UtcNow,
			cancellationToken);

		if (blockedSubjectIds.Count == 0)
		{
			return atsRecords;
		}

		var blocked = blockedSubjectIds.ToHashSet();

		return atsRecords
			.Where(record => !blocked.Contains(record.SubjectId))
			.ToList();
	}

	public async Task<IReadOnlyList<SentVerificationRequestDTO>> ListSentRequestsAsync(
		CancellationToken cancellationToken)
	{
		var requests = await _repository.ListAsync(cancellationToken);

		return requests
			.Select(SentVerificationRequestDTO.FromEntity)
			.ToList();
	}

	public async Task<EmploymentVerificationRequest> CreateAndSendAsync(
		CreateEmploymentVerificationRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.CandidateName) ||
			string.IsNullOrWhiteSpace(request.PreviousEmployer) ||
			string.IsNullOrWhiteSpace(request.HrEmail))
		{
			throw new ArgumentException("Candidate, previous employer, and HR email are required.");
		}

		var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
			.Replace("+", "-")
			.Replace("/", "_")
			.TrimEnd('=');
		var now = DateTime.UtcNow;
		var hashToken = _hashService.Hash(token);
		var entity = new EmploymentVerificationRequest
		{
			Id = Guid.NewGuid(),
			AtsSubjectId = request.AtsSubjectId,
			CandidateName = request.CandidateName,
			PreviousEmployer = request.PreviousEmployer,
			Position = request.Position,
			HrEmail = request.HrEmail,
			EmploymentStartDate = ToUtc(request.EmploymentStartDate),
			EmploymentEndDate = ToUtc(request.EmploymentEndDate),
			RequestedAt = now,
			TokenExpiresAt = now.AddHours(_tokenExpiryHours),
			VerificationTokenHash = hashToken
		};

		await _repository.AddAsync(entity, cancellationToken);

		var verificationLink = $"{_applicationformBaseUrl}/{hashToken}";
		var body = $"""
			<!DOCTYPE html>
			<html lang='en'>
			<body style='margin:0;background:#fff5fb;font-family:Arial,sans-serif;color:#321b35'>
			  <div style='max-width:640px;margin:32px auto;background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 12px 35px rgba(169,54,119,.14)'>
				<div style='padding:34px 36px;background:linear-gradient(120deg,#8d2f91 0%,#d945a0 52%,#ff8fb8 100%);color:#ffffff'>
				  <div style='font-size:12px;letter-spacing:2px;text-transform:uppercase;opacity:.86'>CIBI · Employment Verification</div>
				  <h1 style='margin:14px 0 8px;font-size:28px;line-height:1.2'>Please confirm employment</h1>
				  <p style='margin:0;font-size:15px;line-height:1.6'>A former employee listed you as an HR contact.</p>
				</div>
				<div style='padding:34px 36px'>
				  <p style='font-size:16px;line-height:1.7'>Hello,</p>
				  <p style='font-size:16px;line-height:1.7'>Please confirm whether the following employment information is accurate.</p>
				  <table role='presentation' style='width:100%;border-collapse:collapse;margin:24px 0;background:#fff8fc;border:1px solid #f3d8e8;border-radius:12px'>
					<tr><td style='padding:12px 16px;color:#8a6483;font-size:13px'>Applicant</td><td style='padding:12px 16px;font-weight:bold'>{entity.CandidateName}</td></tr>
					<tr><td style='padding:12px 16px;color:#8a6483;font-size:13px'>Previous employer</td><td style='padding:12px 16px;font-weight:bold'>{entity.PreviousEmployer}</td></tr>
					<tr><td style='padding:12px 16px;color:#8a6483;font-size:13px'>Position</td><td style='padding:12px 16px;font-weight:bold'>{entity.Position}</td></tr>
					<tr><td style='padding:12px 16px;color:#8a6483;font-size:13px'>Employment period</td><td style='padding:12px 16px;font-weight:bold'>{entity.EmploymentStartDate:MMM yyyy} – {entity.EmploymentEndDate:MMM yyyy}</td></tr>
				  </table>
				  <p style='font-size:15px;line-height:1.6'>Choose one response below. This secure link can be used once and expires in 72 hours.</p>
				  <p style='margin:28px 0;text-align:center'><a href='{verificationLink}' style='display:inline-block;padding:14px 26px;border-radius:999px;background:linear-gradient(120deg,#a52d91,#e3489f);color:#ffffff;text-decoration:none;font-weight:bold'>Confirm employment details</a></p>
				  <p style='font-size:12px;line-height:1.6;color:#8a7186;text-align:center'>If you cannot confirm this information, open the link and choose the rejection option.</p>
				</div>
				<div style='padding:20px 36px;background:#fff8fc;color:#95758f;font-size:12px;line-height:1.6'>This is an automated request from CIBI. If you did not receive this request in your HR capacity, you may disregard this message.</div>
			  </div>
			</body>
			</html>
			""";

		if (!await _emailService.SendEmailAsync(
				entity.HrEmail,
				"Employment verification request",
				body,
				true))
		{
			throw new InvalidOperationException("The verification email could not be sent.");
		}

		var sentAt = DateTime.UtcNow;
		await _repository.MarkSentAsync(entity.Id, sentAt, cancellationToken);

		entity.Status = VerificationRequestStatus.Sent;
		entity.SentAt = sentAt;

		return entity;
	}

	public async Task<EmploymentVerificationCompletionResult> VerifyAsync(
		string token,
		bool reject,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return EmploymentVerificationCompletionResult.NotFound();
		}

		// The emailed link carries the stored hash itself, so it is matched
		// directly here and in GetPreviewByTokenAsync without re-hashing.
		var entity = await _repository.FindByTokenHashAsync(token, cancellationToken);

		if (entity is null)
		{
			return EmploymentVerificationCompletionResult.NotFound();
		}

		if (entity.TokenExpiresAt < DateTime.UtcNow)
		{
			return EmploymentVerificationCompletionResult.Expired();
		}

		// Single use is enforced by the terminal status, not by destroying the
		// hash: the row must stay findable so a second click can be told the
		// link was already answered instead of being reported as unknown.
		if (entity.Status is VerificationRequestStatus.Verified
			or VerificationRequestStatus.Rejected)
		{
			return EmploymentVerificationCompletionResult.AlreadyCompleted(
				EmploymentVerificationPreviewDTO.FromEntity(entity));
		}

		var respondedAt = DateTime.UtcNow;
		var status = reject
			? VerificationRequestStatus.Rejected
			: VerificationRequestStatus.Verified;

		// The update only matches a row that is still awaiting a response, so two
		// simultaneous clicks cannot both be recorded. Losing that race is the
		// same outcome as the status check above: already answered.
		if (!await _repository.MarkRespondedAsync(
				entity.Id,
				status,
				respondedAt,
				cancellationToken))
		{
			return EmploymentVerificationCompletionResult.AlreadyCompleted(
				EmploymentVerificationPreviewDTO.FromEntity(entity));
		}

		entity.Status = status;
		entity.VerifiedAt = reject ? null : respondedAt;
		entity.RejectedAt = reject ? respondedAt : null;

		return EmploymentVerificationCompletionResult.Completed(
			EmploymentVerificationPreviewDTO.FromEntity(entity));
	}

	public async Task<EmploymentVerificationPreviewResult> GetPreviewByTokenAsync(
		string token,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return EmploymentVerificationPreviewResult.NotFound();
		}

		var entity = await _repository.FindByTokenHashAsync(
			token,
			cancellationToken);

		if (entity is null)
		{
			return EmploymentVerificationPreviewResult.NotFound();
		}


		// Check the token before exposing any request details: an expired or spent
		// link must not disclose the candidate or employer to the caller.
		if (entity.TokenExpiresAt < DateTime.UtcNow)
		{
			return EmploymentVerificationPreviewResult.Expired();
		}

		if (entity.Status is VerificationRequestStatus.Verified
			or VerificationRequestStatus.Rejected)
		{
			return EmploymentVerificationPreviewResult.AlreadyCompleted();
		}

		return EmploymentVerificationPreviewResult.Valid(
			EmploymentVerificationPreviewDTO.FromEntity(entity));
	}

	private static DateTime? ToUtc(DateTime? value)
	{
		if (value is null)
		{
			return null;
		}

		return value.Value.Kind switch
		{
			DateTimeKind.Utc => value.Value,
			DateTimeKind.Local => value.Value.ToUniversalTime(),
			_ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
		};
	}
}
