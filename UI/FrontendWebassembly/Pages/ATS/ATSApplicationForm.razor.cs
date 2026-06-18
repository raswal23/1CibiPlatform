namespace FrontendWebassembly.Pages.ATS;

public partial class ATSApplicationForm
{
	private bool _showApplicationForm = false;
	private string? Status = string.Empty;
	private bool IsExpired = false;
	// [Parameter]
	// public string? HashToken { get; set; }
	[Parameter]
	[SupplyParameterFromQuery(Name = "philSysShow")]
	public bool philSysShow { get; set; }
	[Parameter]
	[SupplyParameterFromQuery(Name = "stepActive")]
	public int stepActive { get; set; }

	protected override async Task OnInitializedAsync()
	{
		// var response = await ATSService.GetEmailIdAndApplicationFormPathAsync(HashToken!);
		// Status = response?.Status;
		// IsExpired = response!.IsExpired;
		//if expired clear localstorage
	}

	private void Proceed()
	{
		_showApplicationForm = true;
	}
}
