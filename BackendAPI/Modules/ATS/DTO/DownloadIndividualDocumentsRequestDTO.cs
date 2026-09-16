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
	public const string NbiClearance = "NbiClearance";
	public const string Diploma = "Diploma";

	// "Coe" is kept for older callers and resolves to the first COE on record;
	// the numbered types address each employer's COE individually.
	public const string Coe = "Coe";
	public const string Coe1 = "Coe1";
	public const string Coe2 = "Coe2";
	public const string Coe3 = "Coe3";
	public const string ConsentForm = "ConsentForm";
	public const string Report = "Report";

	// Not individually requestable (absent from All); used only to label documents
	// in the compiled multi-order download.
	public const string License = "License";

	public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		BiometricPhoto, Resume, GovernmentId, NbiClearance, Diploma, Coe, Coe1, Coe2, Coe3, ConsentForm, Report
	};
}
