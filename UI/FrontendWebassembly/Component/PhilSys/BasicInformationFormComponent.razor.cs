namespace FrontendWebassembly.Component.PhilSys;

public partial class BasicInformationFormComponent
{
	private IdentityData identityData = new();
	private MudForm? personalForm;
	private DateTime? BirthDate { get; set; }
	[Parameter] public string? HashToken { get; set; }


	private async Task SubmitPersonalInfo()
	{
		await personalForm!.ValidateAsync();
		if (!personalForm.IsValid)
		{
			return;
		}

		identityData.ats_session = HashToken;
		identityData!.birth_date = BirthDate?.ToString("yyyy-MM-dd");

		Console.WriteLine(identityData!.birth_date);

		var livenessLink = await PhilSysService.PostBasicInformationOrPCNAsync("name_dob", identityData!);
		if (!string.IsNullOrEmpty(livenessLink))
		{
			Navigation.NavigateTo(livenessLink, false);
		}
	}
}
