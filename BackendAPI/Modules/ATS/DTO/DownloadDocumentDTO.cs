namespace ATS.DTO;

public sealed class DownloadDocumentDTO
{
	public Guid EmailInvitationRequestId { get; set; }

	public string SubjectName { get; set; } = default!;

	public string FileName { get; set; } = default!;

	public string FileKey { get; set; } = default!;
}