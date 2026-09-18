namespace FrontendWebassembly.Component.UserManagement;

/// <summary>
/// Approve or reject one user awaiting approval.
/// </summary>
/// <remarks>
/// Closes with <see cref="UserApprovalDecision"/> rather than the DTO alone, because the two
/// buttons are different operations - approve PATCHes the user, reject deletes them off the
/// queue - and the caller cannot tell them apart from the user record. Cancelling still closes
/// with Canceled, so the X, the backdrop and Cancel stay distinct from a rejection.
/// </remarks>
public partial class EditUserApprovalComponent
{
	private MudForm? EditUserApprovalForm;

	[CascadingParameter] IMudDialogInstance? EditUserApprovalDialog { get; set; }

	[Parameter] public UnApprovedUsersDTO User { get; set; } = new UnApprovedUsersDTO();

	void Cancel() => EditUserApprovalDialog!.Cancel();

	async Task Submit()
	{
		User.isApproved = true;
		await EditUserApprovalForm!.ValidateAsync();
		if (EditUserApprovalForm!.IsValid)
		{
			EditUserApprovalDialog!.Close(DialogResult.Ok(
				new UserApprovalDecision(UserApprovalAction.Approve, User)));
		}
	}

	/// <summary>
	/// Rejecting needs no form validation: the dialog's only field is the read-only email, and
	/// the decision is about the user as they already are.
	/// </summary>
	void Disapprove() =>
		EditUserApprovalDialog!.Close(DialogResult.Ok(
			new UserApprovalDecision(UserApprovalAction.Reject, User)));
}

public enum UserApprovalAction
{
	Approve,
	Reject
}

public record UserApprovalDecision(UserApprovalAction Action, UnApprovedUsersDTO User);
