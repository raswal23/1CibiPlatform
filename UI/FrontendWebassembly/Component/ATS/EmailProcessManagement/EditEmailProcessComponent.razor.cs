using FrontendWebassembly.ShareData.ATS;
using FrontendWebassembly.SharedService;

namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// Edits one notice's copy list. Closes with the DTO; the page does the posting.
/// </summary>
/// <remarks>
/// The name is editable, and that is the part worth being careful with: a notice reads its copy
/// list by matching this exact string, so changing it does not retitle the row - it moves the
/// addresses onto a different notice and leaves the original copying nobody. The dialog says so out
/// loud the moment the selection differs from what was loaded, because the alternative is an
/// operator discovering it from a team that stopped receiving mail.
///
/// Built from the row in <see cref="OnParametersSet"/> rather than bound to it, so cancelling
/// leaves the table's copy untouched.
/// </remarks>
public partial class EditEmailProcessComponent
{
	private MudForm? _editEmailProcessForm;
	private EmailCopyListInput? _copyListInput;

	[CascadingParameter] private IMudDialogInstance? EditEmailProcessDialog { get; set; }

	[Parameter] public EmailProcessDetailsDTO Process { get; set; } = new();

	private readonly EditEmailProcessDTO _edit = new();

	// False until Save is pressed, so the dialog does not open already arguing with the operator.
	private bool ShowValidation { get; set; }

	private bool IsRenamed =>
		_edit.EmailProcess.Length > 0
		&& !string.Equals(_edit.EmailProcess, Process.EmailProcess, StringComparison.Ordinal);

	/// <summary>
	/// The combination the server refuses. Measured on the DTO rather than on the chip input's own
	/// count, because the DTO is what actually gets posted.
	/// </summary>
	private bool EmptyActiveProblem =>
		_edit.IsActive
		&& EmailCopyListDraft.Split(_edit.CCEmail).Count == 0;

	private string StatusExplanation
	{
		get
		{
			if (!_edit.IsActive)
			{
				return "Kept on file, but nobody is copied on this notice";
			}

			return EmptyActiveProblem
				? "Needs at least one address before it can be switched on"
				: "Every notice of this kind copies these mailboxes";
		}
	}

	protected override void OnParametersSet()
	{
		_edit.Id = Process.Id;
		_edit.EmailProcess = Process.EmailProcess;
		_edit.CCEmail = Process.CCEmail;
		_edit.IsActive = Process.IsActive;
	}

	private void ToggleStatus() => _edit.IsActive = !_edit.IsActive;

	private void Cancel() => EditEmailProcessDialog!.Cancel();

	private async Task Submit()
	{
		await _editEmailProcessForm!.ValidateAsync();

		ShowValidation = true;

		// The chip input reports a malformed address itself, as the operator types it. Re-checked
		// here so Save cannot close the dialog with one still in the box.
		if (!_editEmailProcessForm.IsValid
			|| EmptyActiveProblem
			|| _copyListInput?.HasError == true)
		{
			return;
		}

		EditEmailProcessDialog!.Close(DialogResult.Ok(_edit));
	}
}
