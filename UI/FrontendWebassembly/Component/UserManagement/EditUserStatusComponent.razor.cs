namespace FrontendWebassembly.Component.UserManagement;

/// <summary>
/// Activates or deactivates one registered user.
/// </summary>
/// <remarks>
/// Deliberately narrower than an edit dialog: the account's name, email and approval are
/// displayed but never editable, because approval belongs to the Approval tab and the rest
/// belongs to the user. Closes with an <see cref="EditUserStatusDTO"/>; the page performs
/// the call, matching the other User Management dialogs.
/// </remarks>
public partial class EditUserStatusComponent
{
	[CascadingParameter]
	private IMudDialogInstance? EditUserStatusDialog { get; set; }

	[Parameter]
	[EditorRequired]
	public UsersDTO User { get; set; } = new();

	private bool IsActive;

	private string DisplayName
	{
		get
		{
			var name = string.Join(
				" ",
				new[] { User.firstName, User.middleName, User.lastName }
					.Where(part => !string.IsNullOrWhiteSpace(part)));

			// Legacy rows carry empty name parts, and a blank line reads as a broken
			// dialog rather than a user without a name on file.
			return string.IsNullOrWhiteSpace(name) ? "Unnamed user" : name;
		}
	}

	// Saving an unchanged status would spend a request and a table reload to write the
	// value already there, so the button stays disabled until the toggle actually moves.
	private bool HasChanged => IsActive != User.isActive;

	protected override void OnParametersSet() => IsActive = User.isActive;

	private void ToggleStatus() => IsActive = !IsActive;

	private void Cancel() => EditUserStatusDialog!.Cancel();

	private void Submit()
	{
		if (!HasChanged)
		{
			return;
		}

		EditUserStatusDialog!.Close(DialogResult.Ok(new EditUserStatusDTO
		{
			UserId = User.userId,
			IsActive = IsActive
		}));
	}
}
