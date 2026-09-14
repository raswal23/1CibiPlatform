namespace FrontendWebassembly.Pages.ATS;

public partial class ATSApplicationForm
{
	private bool _showApplicationForm = false;
	private bool _showPhilsys = false;
	private int _stepActive = 0;
	private string? _initError;
	private string? Status;
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
	private DateOnly? _orderDateOfBirth;
	private bool IsInstructionsVisible =>
		!_showApplicationForm &&
		!string.Equals(Status, "Done", StringComparison.OrdinalIgnoreCase) &&
		!string.Equals(Status, "Withdrawn", StringComparison.OrdinalIgnoreCase);
	private string RootCssClass =>
		$"ats-application-form ats-appform-modern{(IsInstructionsVisible ? " ats-application-intro-host" : string.Empty)}";

	protected override async Task OnInitializedAsync()
	{
		var response = await ATSService.GetEmailIdAndApplicationFormPathAsync(HashToken!);

		if (!response.IsSuccess)
		{
			_initError = response.ErrorDetail;
			return;
		}

		var details = response.Data!;
		Status = details.Status;
		EmailId = details.EmailId;
		_orderDateOfBirth = details.DateOfBirth;

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
	}

	private async Task ConfirmNavigation(LocationChangingContext context)
	{
		if (hasUnsavedChanges)
		{
			var result = await JSRuntime.InvokeAsync<bool>("confirm",
				"Are you sure you want to proceed?");

			if (!result)
			{
				context.PreventNavigation();
			}
		}
	}

	private void SetWithdrawnStatus(string value)
	{
		Status = value;
	}

	private void ShowApplicationForm()
	{
		_showApplicationForm = true;
	}

	private void SetDirtyState(bool value)
	{
		hasUnsavedChanges = value;
	}
}
