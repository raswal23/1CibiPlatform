using FrontendWebassembly.ShareData.ATS;
using FrontendWebassembly.SharedService;

namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// Registers the copy list for a notice that does not have one yet. Closes with the DTO; the page
/// does the posting.
/// </summary>
/// <remarks>
/// Active by default, because an operator registering a copy list means to use it. That default is
/// what makes the empty-and-active rule visible straight away: the server rejects an active row
/// with no addresses, and it is the one combination that fails at SEND time rather than at save
/// time - the notice would hand an empty string to <c>MimeKit.MailboxAddress.Parse</c>. Saying so
/// here is cheaper than discovering it in a failed batch.
/// </remarks>
public partial class AddEmailProcessComponent
{
	private MudForm? _addEmailProcessForm;
	private EmailCopyListInput? _copyListInput;

	[CascadingParameter] private IMudDialogInstance? AddEmailProcessDialog { get; set; }

	private readonly AddEmailProcessDTO _newEmailProcess = new()
	{
		IsActive = true
	};

	// False until Save is pressed, so the dialog does not open already arguing with the operator.
	private bool ShowValidation { get; set; }

	/// <summary>
	/// The combination the server refuses. Measured on the DTO rather than on the chip input's own
	/// count, because the DTO is what actually gets posted - if the two ever disagreed, this would
	/// be blocking a save the server would have accepted.
	/// </summary>
	private bool EmptyActiveProblem =>
		_newEmailProcess.IsActive
		&& EmailCopyListDraft.Split(_newEmailProcess.CCEmail).Count == 0;

	private string StatusExplanation
	{
		get
		{
			if (!_newEmailProcess.IsActive)
			{
				return "Kept on file, but nobody is copied on this notice";
			}

			return EmptyActiveProblem
				? "Needs at least one address before it can be switched on"
				: "Every notice of this kind copies these mailboxes";
		}
	}

	private void ToggleStatus() => _newEmailProcess.IsActive = !_newEmailProcess.IsActive;

	private void Cancel() => AddEmailProcessDialog!.Cancel();

	private async Task Submit()
	{
		await _addEmailProcessForm!.ValidateAsync();

		ShowValidation = true;

		// The chip input reports a malformed address itself, as the operator types it. Re-checked
		// here so Save cannot close the dialog with one still in the box.
		if (!_addEmailProcessForm.IsValid
			|| EmptyActiveProblem
			|| _copyListInput?.HasError == true)
		{
			return;
		}

		AddEmailProcessDialog!.Close(DialogResult.Ok(_newEmailProcess));
	}
}
