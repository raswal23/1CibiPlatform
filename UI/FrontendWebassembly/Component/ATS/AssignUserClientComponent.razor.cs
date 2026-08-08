namespace FrontendWebassembly.Component.ATS;

public partial class AssignUserClientComponent
{
	private MudForm? AssignmentForm;

	[CascadingParameter]
	private IMudDialogInstance? AssignClientDialog { get; set; }

	[Parameter]
	public IReadOnlyList<ATSUserLookupDTO> AuthUsers { get; set; } = Array.Empty<ATSUserLookupDTO>();

	[Parameter]
	public IReadOnlyList<ClientDetailsDTO> Clients { get; set; } = Array.Empty<ClientDetailsDTO>();

	[Parameter]
	public IReadOnlyList<UserClientDetailsDTO> Assignments { get; set; } = Array.Empty<UserClientDetailsDTO>();

	private AssignATSUserClientDTO Assignment { get; set; } = new();
	private ATSUserLookupDTO? SelectedAuthUser { get; set; }
	private string? AuthUserError { get; set; }
	private string? ClientError { get; set; }

	private void Cancel() => AssignClientDialog!.Cancel();

	private async Task Submit()
	{
		await AssignmentForm!.ValidateAsync();
		AuthUserError = SelectedAuthUser is null || Assignment.UserId == Guid.Empty
			? "User is required"
			: null;
		ClientError = Assignment.ClientId <= 0 ? "Client is required" : null;

		if (AssignmentForm.IsValid && AuthUserError is null && ClientError is null)
			AssignClientDialog!.Close(DialogResult.Ok(Assignment));
	}

	private void OnAuthUserChanged(ATSUserLookupDTO? authUser)
	{
		SelectedAuthUser = authUser;
		Assignment.UserId = authUser?.UserId ?? Guid.Empty;
		Assignment.ClientId = authUser is null
			? 0
			: Assignments.FirstOrDefault(item => item.UserId == authUser.UserId)?.ClientId ?? 0;
		AuthUserError = authUser is null ? "User is required" : null;
		ClientError = null;
	}

	private Task<IEnumerable<ATSUserLookupDTO>> SearchAuthUsers(
		string value,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		IEnumerable<ATSUserLookupDTO> users = AuthUsers;
		if (!string.IsNullOrWhiteSpace(value))
		{
			users = users.Where(user =>
				user.UserName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
				user.UserEmail.Contains(value, StringComparison.OrdinalIgnoreCase));
		}

		return Task.FromResult(users);
	}

	private static string GetAuthUserText(ATSUserLookupDTO? user)
	{
		return user is null ? string.Empty : $"{user.UserName} ({user.UserEmail})";
	}
}
