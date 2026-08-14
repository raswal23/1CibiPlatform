namespace ATS.Shared.Contracts;

public sealed record ATSInProgressEmploymentRecord(
    Guid SubjectId,
    string CandidateName,
    string Employer,
    string? Position,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? HrName,
    string? HrEmail);

public interface IATSVerificationDataProvider
{
    Task<IReadOnlyList<ATSInProgressEmploymentRecord>> GetInProgressEmploymentAsync(CancellationToken cancellationToken = default);
}
