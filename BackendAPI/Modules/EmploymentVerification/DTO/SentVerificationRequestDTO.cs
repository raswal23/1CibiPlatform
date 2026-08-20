namespace EmploymentVerification.DTO;

/// <summary>
/// Projection of a verification request for the authenticated tracking view.
/// Deliberately excludes <see cref="EmploymentVerificationRequest.VerificationTokenHash"/>,
/// which is the credential embedded in the emailed link and must never be
/// returned to a list caller.
/// </summary>
public sealed record SentVerificationRequestDTO(
	Guid RequestId,
	Guid? SubjectId,
	string CandidateName,
	string PreviousEmployer,
	string Position,
	DateTime? EmploymentStartDate,
	DateTime? EmploymentEndDate,
	string? HrName,
	string HrEmail,
	string Status,
	DateTime RequestedAt,
	DateTime? SentAt,
	DateTime? VerifiedAt,
	DateTime? RejectedAt,
	DateTime TokenExpiresAt)
{
	/// <summary>
	/// Maps the persisted request onto the tracking contract. <c>Status</c> is
	/// emitted as the enum name because the UI transport model types it as a string.
	/// </summary>
	public static SentVerificationRequestDTO FromEntity(
		EmploymentVerificationRequest entity) =>
		new(
			RequestId: entity.Id,
			SubjectId: entity.AtsSubjectId,
			CandidateName: entity.CandidateName,
			PreviousEmployer: entity.PreviousEmployer,
			Position: entity.Position,
			EmploymentStartDate: entity.EmploymentStartDate,
			EmploymentEndDate: entity.EmploymentEndDate,
			HrName: entity.HrName,
			HrEmail: entity.HrEmail,
			Status: entity.Status.ToString(),
			RequestedAt: entity.RequestedAt,
			SentAt: entity.SentAt,
			VerifiedAt: entity.VerifiedAt,
			RejectedAt: entity.RejectedAt,
			TokenExpiresAt: entity.TokenExpiresAt);
}
