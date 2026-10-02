namespace ATS.DTO;

/// <summary>
/// What a bulk retry actually did.
///
/// Both numbers are reported because they routinely differ, and the difference is the
/// interesting part: a selection made a minute ago can contain rows the background job has
/// since picked up on its own. Those are skipped rather than raced, so "3 of 5 requeued" is
/// a normal, correct outcome - not a partial failure - and the operator needs to see it
/// rather than be told "done".
/// </summary>
public sealed class BulkRetryResultDTO
{
	public int RequestedCount { get; set; }

	public int RequeuedCount { get; set; }

	/// <summary>
	/// True when everything the caller asked for moved, so the UI can pick a plain success
	/// message instead of explaining the shortfall.
	/// </summary>
	public bool IsComplete => RequeuedCount == RequestedCount;
}
