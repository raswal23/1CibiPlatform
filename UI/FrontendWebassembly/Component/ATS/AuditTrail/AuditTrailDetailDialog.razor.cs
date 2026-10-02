using System.Text.Json;

namespace FrontendWebassembly.Component.ATS;

public partial class AuditTrailDetailDialog
{
	// Mirrors AtsAuditRedactor.Mask, which lives in the backend assembly. Shown so the
	// reader knows a masked value was never stored rather than merely hidden here.
	private const string Mask = "***";

	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public AuditTrailListDTO Entry { get; set; } = default!;

	private string? _copiedValue;

	private string HeaderSubtitle =>
		$"{DisplayName} · {Entry.Area} · {AbsoluteTime}";

	// The account may since have been renamed or removed, so the trail falls back to
	// whatever identifier it captured at the time.
	private string DisplayName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Entry.UserFullName))
			{
				return Entry.UserFullName;
			}

			return string.IsNullOrWhiteSpace(Entry.UserEmail)
				? "Unknown user"
				: Entry.UserEmail;
		}
	}

	private string OutcomeClass => Entry.Outcome switch
	{
		AuditActionOutcome.Success => "done",
		AuditActionOutcome.Failure => "error",
		_ => "unknown"
	};

	private string AbsoluteTime =>
		DateTime.SpecifyKind(Entry.OccurredAt, DateTimeKind.Utc)
			.ToLocalTime()
			.ToString("MMMM dd, yyyy h:mm:ss tt");

	private string Duration =>
		Entry.DurationMs < 1000
			? $"{Entry.DurationMs} ms"
			: $"{Entry.DurationMs / 1000.0:0.0} s";

	// Recorded as they were when the action was taken; the user's current role or client
	// may differ, which is the point of denormalising them onto the entry.
	private string RoleAndClient
	{
		get
		{
			if (Entry.IsPlatformSuperAdmin)
			{
				return "Platform super admin";
			}

			var role = Entry.AtsRoleId?.ToString() ?? "—";
			var client = Entry.AtsClientId?.ToString() ?? "—";

			return $"Role {role} · Client {client}";
		}
	}

	/// <summary>
	/// The field-level diffs, deserialized from the entry's Changes JSON. Empty when the
	/// action produced none, which the dialog reports rather than hiding.
	/// </summary>
	private IReadOnlyList<AuditEntityChangeDTO> EntityChanges
	{
		get
		{
			if (string.IsNullOrWhiteSpace(Entry.Changes))
			{
				return [];
			}

			try
			{
				return JsonSerializer.Deserialize<List<AuditEntityChangeDTO>>(
					Entry.Changes,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
			}
			catch (JsonException)
			{
				// An oversized diff is stored as a marker object, not an array; it fails
				// to deserialize here and falls through to the note below.
				return [];
			}
		}
	}

	// Distinguishes "we could not capture this" from "nothing changed", which are very
	// different answers to an auditor.
	private string NoChangesReason =>
		string.IsNullOrWhiteSpace(Entry.Changes)
			? "Field-level changes are not captured for this action type. Actions that write "
				+ "directly to the database - ticketing and email status updates - bypass the "
				+ "change tracker this record is built from."
			: "This action changed too many records to list here. The request itself is shown below.";

	private static string StateLabel(string state) => state switch
	{
		"Added" => "Created",
		"Modified" => "Updated",
		"Deleted" => "Removed",
		_ => state
	};

	// Re-indented for reading. A payload that is not valid JSON - the "omitted" markers
	// are, but a future one might not be - is shown verbatim rather than swallowed.
	// Matched on the action rather than by sniffing the payload's shape: the writer sets
	// this name in AtsAssistantService.RecordAudit, and a JSON probe would misfire on any
	// future command that happens to carry a "Question" field.
	private bool IsAssistantTranscript =>
		string.Equals(Entry.Action, "AskAtsAssistant", StringComparison.OrdinalIgnoreCase);

	private string FormattedPayload
	{
		get
		{
			var payload = Entry.Payload ?? string.Empty;

			try
			{
				using var document = JsonDocument.Parse(payload);

				return JsonSerializer.Serialize(
					document.RootElement,
					new JsonSerializerOptions { WriteIndented = true });
			}
			catch (JsonException)
			{
				return payload;
			}
		}
	}

	private static string Fallback(string? value) =>
		string.IsNullOrWhiteSpace(value) ? "—" : value;

	private bool IsCopied(string? value) =>
		!string.IsNullOrEmpty(value)
		&& string.Equals(_copiedValue, value, StringComparison.Ordinal);

	private async Task CopyValueAsync(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}

		try
		{
			await JS.InvokeVoidAsync("navigator.clipboard.writeText", value);

			_copiedValue = value;
			StateHasChanged();

			// Clears the "copied" state so the button does not stay lit indefinitely.
			await Task.Delay(1500);

			if (string.Equals(_copiedValue, value, StringComparison.Ordinal))
			{
				_copiedValue = null;
				StateHasChanged();
			}
		}
		catch (JSException)
		{
			// Clipboard access can be refused by the browser; the value is still on
			// screen to select by hand, so this is not worth interrupting the user for.
		}
	}

	private void Cancel() => MudDialog.Cancel();
}
