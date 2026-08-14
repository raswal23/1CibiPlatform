using FrontendWebassembly.DTO.EmploymentVerification;

namespace FrontendWebassembly.Services.EmploymentVerification.Interface;

public interface IEmploymentVerificationService
{
    Task<EmploymentVerificationResponseDTO<IReadOnlyList<EmploymentVerificationRequestDTO>>> GetRequestsAsync(
        CancellationToken cancellationToken = default);

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
