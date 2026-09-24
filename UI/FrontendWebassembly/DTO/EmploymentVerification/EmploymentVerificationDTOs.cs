namespace FrontendWebassembly.DTO.EmploymentVerification;

public sealed record EmploymentVerificationResponseDTO<T>(
	T? Data,
	string Detail,
	string ErrorMessage);

/// <summary>
/// Why a token-backed call could not be served. Mapped from the API problem
/// response title so the confirmation page can render a tailored state instead
/// of a generic error.
/// </summary>
public enum VerificationLinkFailure
{
	None,
	Expired,
	AlreadyUsed,
	NotFound,
	Unknown
}

/// <summary>
/// Envelope for the anonymous verification calls, carrying the failure reason
/// alongside the payload.
/// </summary>
public sealed record VerificationLinkResultDTO<T>(
	T? Data,
	string ErrorMessage,
	VerificationLinkFailure Failure);

/// <summary>
/// Transport model for the tracking view, returned by
/// <c>GET /employmentverification/getsentrequests</c>. Mirrors the API's
/// <c>SentVerificationRequestDTO</c>, which excludes the verification token hash.
/// </summary>
public sealed class SentVerificationRequestDTO
{
	public Guid RequestId { get; set; }
	public Guid? SubjectId { get; set; }
	public string CandidateName { get; set; } = "";
	public string PreviousEmployer { get; set; } = "";
	public string Position { get; set; } = "";
	public DateTime? EmploymentStartDate { get; set; }
	public DateTime? EmploymentEndDate { get; set; }
	public string? HrName { get; set; }
	public string HrEmail { get; set; } = "";

	/// <summary>
	/// Where <see cref="HrEmail"/> came from - "Directory" for a vetted company
	/// mailbox, "CandidateSupplied" for the address the candidate typed. Null on
	/// requests raised before sending was automated.
	/// </summary>
	public string? RecipientSource { get; set; }

	/// <summary>Which of the form's three employer slots this request covers.</summary>
	public short? EmploymentSegment { get; set; }

	public string Status { get; set; } = "";
	public DateTime RequestedAt { get; set; }
	public DateTime? SentAt { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public DateTime? RejectedAt { get; set; }
	public DateTime TokenExpiresAt { get; set; }
}

/// <summary>
/// Transport model for the anonymous token preview returned by
/// <c>GET /employmentverification/preview/{token}</c>.
/// </summary>
public sealed class EmploymentVerificationPreviewDTO
{
	public Guid RequestId { get; set; }
	public Guid? SubjectId { get; set; }
	public string CandidateName { get; set; } = "";
	public string PreviousEmployer { get; set; } = "";
	public string Position { get; set; } = "";
	public DateTime? EmploymentStartDate { get; set; }
	public DateTime? EmploymentEndDate { get; set; }
	public string? HrName { get; set; }
	public string HrEmail { get; set; } = "";
	public string Status { get; set; } = "";
	public DateTime RequestedAt { get; set; }
	public DateTime? SentAt { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public DateTime? RejectedAt { get; set; }
	public DateTime TokenExpiresAt { get; set; }
}

public sealed class EmploymentVerificationResponseDetailsDTO
{
	public string CandidateName { get; set; } = "";
}

public sealed class CreateEmploymentVerificationRequestDTO
{
	public Guid? AtsSubjectId { get; set; }
	public string CandidateName { get; set; } = "";
	public string PreviousEmployer { get; set; } = "";
	public string Position { get; set; } = "";
	public string HrEmail { get; set; } = "";
	public DateTime? EmploymentStartDate { get; set; }
	public DateTime? EmploymentEndDate { get; set; }
}

/// <summary>
/// One employment slot awaiting a verification request. Mirrors the API's
/// <c>ATSInProgressEmploymentRecord</c>, which returns one record per slot rather than
/// per candidate - a candidate with three former employers appears three times.
/// </summary>
public sealed class ATSInProgressEmploymentRecordDTO
{
	public Guid SubjectId { get; set; }

	/// <summary>Which of the form's three employer slots this is: 1, 2 or 3.</summary>
	public short EmploymentSegment { get; set; }

	public string CandidateName { get; set; } = "";
	public string Employer { get; set; } = "";
	public string? Position { get; set; }
	public DateOnly? StartDate { get; set; }
	public DateOnly? EndDate { get; set; }
	public string? SupervisorName { get; set; }

	/// <summary>
	/// The address the candidate supplied. The sender prefers a vetted directory
	/// mailbox where the company is known, so this is not necessarily who was emailed.
	/// </summary>
	public string? SupervisorEmail { get; set; }

	/// <summary>
	/// Whether the candidate agreed this employer may be contacted. False also covers
	/// "never answered" - the queue treats absence of consent as no.
	/// </summary>
	public bool PermissionToContact { get; set; }
}
