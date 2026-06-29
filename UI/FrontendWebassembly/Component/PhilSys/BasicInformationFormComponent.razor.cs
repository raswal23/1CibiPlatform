namespace FrontendWebassembly.Component.PhilSys;

public partial class BasicInformationFormComponent
{
	private IdentityData identityData = new();
	private MudForm? personalForm;
	[Parameter] public string? HashToken { get; set; }

	private async Task SubmitPersonalInfo()
	{
		await personalForm!.ValidateAsync();
		if (!personalForm.IsValid)
		{
			return;
		}

		identityData.ats_session = HashToken;
		var livenessLink = await PhilSysService.PostBasicInformationOrPCNAsync("name_dob", identityData!);
		if (!string.IsNullOrEmpty(livenessLink))
		{
			Navigation.NavigateTo(livenessLink, false);
		}
	}
}
