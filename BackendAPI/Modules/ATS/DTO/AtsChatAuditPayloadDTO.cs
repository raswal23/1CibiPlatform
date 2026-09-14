namespace ATS.DTO;

/// <summary>
/// One assistant exchange as it is stored in the audit trail's <c>Payload</c> column.
///
/// Both sides are recorded because half a conversation is not a record of it: "who asked
/// what, and what did the system tell them" is the question this exists to answer. The
/// audit pipeline only ever serializes the request, which is why
/// <c>AskAtsAssistantCommand</c> stays <c>[SkipAudit]</c> and
/// <c>AtsAssistantService</c> writes this itself.
///
/// Stored into a jsonb column, so it can be queried directly - for example
/// <c>Payload -&gt;&gt; 'Question' ILIKE '%audit%'</c> to find who asked about the trail.
/// </summary>
public record AtsChatAuditPayloadDTO
{
	/// <summary>What the user typed, verbatim.</summary>
	public string Question { get; set; } = string.Empty;

	/// <summary>What the assistant replied, including a refusal.</summary>
	public string Answer { get; set; } = string.Empty;

	/// <summary>
	/// True when the turn was refused as outside ATS. Recorded as its own flag rather than
	/// inferred from the answer text, so a review can find every refusal without matching
	/// on wording that may change.
	/// </summary>
	public bool WasRefused { get; set; }

	/// <summary>
	/// How many orders the answer surfaced. The rows themselves are not stored - they are
	/// already in the tables this trail sits beside, and duplicating candidate data into
	/// the audit payload would spread it further for no gain.
	/// </summary>
	public int OrderResultCount { get; set; }

	/// <summary>
	/// How many audit entries the answer surfaced. Non-zero means someone used the
	/// assistant to read the audit trail, which is itself worth being able to find.
	/// </summary>
	public int AuditResultCount { get; set; }

	/// <summary>
	/// True when the turn staged an order draft. The draft is not the order - confirming it
	/// raises its own audited ConfirmOrderDraft entry.
	/// </summary>
	public bool StagedOrderDraft { get; set; }
}
