namespace ATS.DTO;

/// <summary>
/// The subject name after an edit. SubjectName is the same concatenation the
/// reports list renders, so the caller can update the row without a refetch.
/// </summary>
public class SubjectNameDTO
{
	public Guid EmailInvitationRequestId { get; set; }

	public string? FirstName { get; set; }

	public string? MiddleInitial { get; set; }

	public string? LastName { get; set; }

	public string SubjectName { get; set; } = string.Empty;
}
