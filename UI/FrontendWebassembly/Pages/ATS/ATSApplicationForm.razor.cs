namespace FrontendWebassembly.Pages.ATS;

public partial class ATSApplicationForm
{
	private bool _showApplicationForm = false;
	private bool _showPhilsys = false;
	private int _stepActive = 0;
	private bool Status;
	private bool IsExpired = false;
	private bool hasUnsavedChanges = true;
	private readonly HashSet<int> allowedSteps = new() { 0, 1, 2, 3, 4, 5 };
	[Parameter]
	public string? HashToken { get; set; }
	[Parameter]
	[SupplyParameterFromQuery(Name = "philSysShow")]
	public string? philSysShow { get; set; }
	[Parameter]
	[SupplyParameterFromQuery(Name = "stepActive")]
	public int stepActive { get; set; }
	[Parameter]
	[SupplyParameterFromQuery(Name = "showAppForm")]
	public string? showAppForm { get; set; }
	public Guid EmailId;

	protected override async Task OnInitializedAsync()
	{
		var response = await ATSService.GetEmailIdAndApplicationFormPathAsync(HashToken!);
		Status = response.Status;
		IsExpired = response!.IsExpired;
		EmailId = response.EmailId;

		_showApplicationForm = showAppForm?.ToLowerInvariant() switch
		{
			"true" => true,
			"false" => false,
			_ => false 
		};

		_showPhilsys = philSysShow?.ToLowerInvariant() switch
		{
			"true" => true,
			"false" => false,
			_ => false
		};

		_stepActive = allowedSteps.Contains(stepActive)
			? stepActive
			: 1;

		if (response.IsExpired)
		{
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:firstName");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:middleName");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:lastName");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:suffix");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:birthDate");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:sex");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:emailAddress");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:phoneNumber");
			await LocalStorageService.RemoveItemAsync($"ats:applicationForm:profilePicture");
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
