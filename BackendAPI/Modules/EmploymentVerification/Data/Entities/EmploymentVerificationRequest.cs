namespace EmploymentVerification.Data.Entities;

public enum VerificationRequestStatus { Pending, Sent, Verified, Rejected, Expired }
public sealed class EmploymentVerificationRequest
{
	public Guid Id { get; set; }
	public Guid? AtsSubjectId { get; set; }
	public string CandidateName { get; set; } = "";
	public string PreviousEmployer { get; set; } = "";
	public string Position { get; set; } = "";
	public DateTime? EmploymentStartDate { get; set; }
	public DateTime? EmploymentEndDate { get; set; }
	public string? HrName { get; set; }
	public string HrEmail { get; set; } = "";
	public VerificationRequestStatus Status { get; set; } = VerificationRequestStatus.Pending;
	public string VerificationTokenHash { get; set; } = "";
	public DateTime TokenExpiresAt { get; set; }
	public DateTime RequestedAt { get; set; }
	public DateTime? SentAt { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public DateTime? RejectedAt { get; set; }
	public string? ResponseNotes { get; set; }
}
