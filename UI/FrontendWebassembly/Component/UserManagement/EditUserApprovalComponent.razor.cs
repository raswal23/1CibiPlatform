namespace FrontendWebassembly.Component.UserManagement;

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
			EditUserApprovalDialog!.Close(DialogResult.Ok(User));
		}
	}
}
