namespace FrontendWebassembly.DTO.ATS;

/// <summary>
/// What a bulk retry actually did. Mirrors the API's response.
///
/// Both numbers are carried because they routinely differ: a selection made a minute ago
/// can contain rows the background job has since picked up on its own, and those are
/// skipped rather than raced. "3 of 5 requeued" is a normal, correct outcome, so the UI
/// has to be able to say so instead of reporting a flat success.
/// </summary>
public class BulkRetryResultDTO
{
	public int RequestedCount { get; set; }

	public int RequeuedCount { get; set; }

	public bool IsComplete { get; set; }
}
