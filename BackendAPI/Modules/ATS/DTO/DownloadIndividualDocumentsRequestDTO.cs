namespace ATS.DTO;

/// <summary>
/// A request to download some of one order's documents.
/// </summary>
/// <remarks>
/// Carries the order id and which kinds of document to include - never object storage
/// keys. The previous shape accepted caller-supplied FileKey values and passed them
/// straight to object storage, which made this endpoint a general-purpose read over the
/// whole bucket for any authenticated user. The server now resolves keys itself, under
/// the caller's access scope.
/// </remarks>
public class DownloadIndividualDocumentsRequestDTO
{
	public Guid EmailInvitationRequestId { get; set; }

	public List<string> DocumentTypes { get; set; } = [];
}

/// <summary>
/// The document kinds a caller may ask for. These are the names on the wire; each maps
/// to a file name/key pair the server looks up.
/// </summary>
public static class AtsDocumentTypes
{
	public const string BiometricPhoto = "BiometricPhoto";
	public const string Resume = "Resume";
	public const string GovernmentId = "GovernmentId";
	public const string Diploma = "Diploma";
	public const string Coe = "Coe";
	public const string ConsentForm = "ConsentForm";
	public const string Report = "Report";

	public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		BiometricPhoto, Resume, GovernmentId, Diploma, Coe, ConsentForm, Report
	};
}
