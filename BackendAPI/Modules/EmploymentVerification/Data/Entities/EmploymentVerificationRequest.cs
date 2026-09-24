namespace EmploymentVerification.Data.Entities;

public enum VerificationRequestStatus { Pending, Sent, Verified, Rejected, Expired }
public sealed class EmploymentVerificationRequest
{
	public Guid Id { get; set; }
	public Guid? AtsSubjectId { get; set; }

	/// <summary>
	/// Which of the application form's three employment slots this request covers
	/// (1, 2 or 3). Null for a request not raised from an ATS record.
	/// </summary>
	/// <remarks>
	/// The form stores all three employers as Emp1*/Emp2*/Emp3* column groups on a
	/// single ats.ProfessionalExperiences row, so every segment of one order shares
	/// an AtsSubjectId. Without this discriminator the availability check cannot tell
	/// them apart, and a request sent for the first employer would be treated as
	/// covering all three - the candidate's other employers would never be contacted.
	/// </remarks>
	public short? EmploymentSegment { get; set; }

	public string CandidateName { get; set; } = "";
	public string PreviousEmployer { get; set; } = "";
	public string Position { get; set; } = "";
	public DateTime? EmploymentStartDate { get; set; }
	public DateTime? EmploymentEndDate { get; set; }
	public string? HrName { get; set; }
	public string HrEmail { get; set; } = "";

	/// <summary>
	/// Where <see cref="HrEmail"/> came from: "Directory" for a vetted company mailbox,
	/// "CandidateSupplied" for the address the candidate typed on the form. Null for
	/// requests raised before sending was automated.
	/// </summary>
	/// <remarks>
	/// Recorded because the two are not equally trustworthy. A candidate can name any
	/// address as their former supervisor, so knowing an outcome came back from a
	/// candidate-supplied mailbox rather than a vetted one is what makes a confirmation
	/// auditable after the fact.
	/// </remarks>
	public string? RecipientSource { get; set; }
	public VerificationRequestStatus Status { get; set; } = VerificationRequestStatus.Pending;
	public string VerificationTokenHash { get; set; } = "";
	public DateTime TokenExpiresAt { get; set; }
	public DateTime RequestedAt { get; set; }
	public DateTime? SentAt { get; set; }
	public DateTime? VerifiedAt { get; set; }
	public DateTime? RejectedAt { get; set; }
	public string? ResponseNotes { get; set; }
}
