using Microsoft.AspNetCore.Components.Routing;

namespace FrontendWebassembly.Pages.ATS;

public partial class ATSApplicationForm
{
	private bool _showApplicationForm = false;
	private bool Status;
	private bool isNavigationLocked = false;
	private bool IsExpired = false;
	private bool hasUnsavedChanges = true;
	[Parameter]
	public string? HashToken { get; set; }
	[Parameter]
	[SupplyParameterFromQuery(Name = "philSysShow")]
	public bool philSysShow { get; set; }
	[Parameter]
	[SupplyParameterFromQuery(Name = "stepActive")]
	public int stepActive { get; set; }
	public Guid EmailId;

	protected override async Task OnInitializedAsync()
	{
		var response = await ATSService.GetEmailIdAndApplicationFormPathAsync(HashToken!);
		Status = response.Status;
		IsExpired = response!.IsExpired;
		EmailId = response.EmailId;

		if (response.IsExpired)
		{
			await LocalStorageService.RemoveItemAsync($"{HashToken}_firstName");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_middleName");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_lastName");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_suffix");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_birthDate");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_sex");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_emailAddress");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_phoneNumber");
			await LocalStorageService.RemoveItemAsync($"{HashToken}_profilePicture");
		}
	}

	private async Task ConfirmNavigation(LocationChangingContext context)
	{
		if (hasUnsavedChanges)
		{
			var result = await JSRuntime.InvokeAsync<bool>("confirm",
				"You have unsaved changes. Leave anyway?");

			if (!result)
			{
				context.PreventNavigation();
			}
		}
	}
	private void SetDirtyState(bool value)
	{
		hasUnsavedChanges = value;
	}
}
