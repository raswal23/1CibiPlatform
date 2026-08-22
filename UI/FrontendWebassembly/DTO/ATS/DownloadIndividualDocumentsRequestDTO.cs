namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// A request to download some of one order's documents. Mirrors the API contract:
/// the order id plus which kinds of document, never object storage keys.
/// </summary>
public class DownloadIndividualDocumentsRequestDTO
{
	public Guid EmailInvitationRequestId { get; set; }

	public List<string> DocumentTypes { get; set; } = [];
}

/// <summary>The document kinds the API accepts. Must match ATS.DTO.AtsDocumentTypes.</summary>
public static class AtsDocumentTypes
{
	public const string BiometricPhoto = "BiometricPhoto";
	public const string Resume = "Resume";
	public const string GovernmentId = "GovernmentId";
	public const string Diploma = "Diploma";
	public const string Coe = "Coe";
	public const string ConsentForm = "ConsentForm";
	public const string Report = "Report";
}
