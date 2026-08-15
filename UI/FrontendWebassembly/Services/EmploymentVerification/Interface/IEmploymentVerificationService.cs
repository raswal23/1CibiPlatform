using FrontendWebassembly.DTO.EmploymentVerification;

namespace FrontendWebassembly.Services.EmploymentVerification.Interface;

public interface IEmploymentVerificationService
{
    /// <summary>
    /// Loads every verification request raised from this module for the tracking
    /// view, with its current status and response timestamps.
    /// </summary>
    Task<EmploymentVerificationResponseDTO<IReadOnlyList<SentVerificationRequestDTO>>> GetSentRequestsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the in-progress ATS candidates that still need a verification email.
    /// Candidates already awaiting a response, or already verified, are filtered
    /// out server side.
    /// </summary>
    Task<EmploymentVerificationResponseDTO<IReadOnlyList<ATSInProgressEmploymentRecordDTO>>> GetInProgressATSRecordsAsync(
        CancellationToken cancellationToken = default);

    Task<EmploymentVerificationResponseDTO<EmploymentVerificationResponseDetailsDTO>> CreateAndSendAsync(
        CreateEmploymentVerificationRequestDTO request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the HR contact's confirmation that the employment details are accurate.
    /// </summary>
    Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>> VerifyAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the HR contact's report that the employment details are inaccurate.
    /// </summary>
    Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>> RejectAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the emailed verification request for the anonymous confirmation page.
    /// The token is validated server side; a failed lookup reports why through
    /// <c>Failure</c> rather than throwing.
    /// </summary>
    Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>> GetPreviewAsync(
        string token,
        CancellationToken cancellationToken = default);
}
