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

public sealed class EmploymentVerificationRequestDTO
{
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

public sealed class ATSInProgressEmploymentRecordDTO
{
    public Guid SubjectId { get; set; }
    public string CandidateName { get; set; } = "";
    public string Employer { get; set; } = "";
    public string? Position { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? HrName { get; set; }
    public string? HrEmail { get; set; }
}
