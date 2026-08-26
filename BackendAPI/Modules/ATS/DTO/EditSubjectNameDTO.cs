namespace ATS.DTO;

/// <summary>
/// Name corrections for one order's subject. Only the name parts are editable —
/// the order itself, its status, and its package are untouched.
/// </summary>
public class EditSubjectNameDTO
{
	public Guid EmailInvitationRequestId { get; set; }

	public string? FirstName { get; set; }

	public string? MiddleInitial { get; set; }

	public string? LastName { get; set; }
}
