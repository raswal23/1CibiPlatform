namespace ATS.DTO;

public sealed class DownloadDocumentDTO
{
	public Guid EmailInvitationRequestId { get; set; }

	public string SubjectName { get; set; } = default!;

	public string FileName { get; set; } = default!;

	public string FileKey { get; set; } = default!;

	// AtsDocumentTypes value, so the compiled download can order the generated
	// application form relative to the stored documents (before the consent form).
	public string? DocumentType { get; set; }
}