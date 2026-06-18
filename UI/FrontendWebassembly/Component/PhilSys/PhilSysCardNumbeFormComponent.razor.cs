namespace FrontendWebassembly.Component.PhilSys;

public partial class PhilSysCardNumbeFormComponent
{
	private IdentityData identityData = new();
	private MudForm? pcnForm;
	[Parameter] public string? HashToken { get; set; }

	private string? ValidatePCN(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "PhilSys Card Number is required";

		string digitsOnly = Regex.Replace(value, @"\D", "");

		identityData!.pcn = digitsOnly;

		if (digitsOnly.Length != 16)
			return "PhilSys Card Number must be exactly 16 digits";

		return null;
	}

	private async Task SubmitPCN()
	{
		await pcnForm!.ValidateAsync();
		if (!pcnForm.IsValid)
		{
			return;
		}

		identityData!.ats_session = HashToken;
		var livenessLink = await PhilSysService.PostBasicInformationOrPCNAsync("pcn", identityData!);
		if (!string.IsNullOrEmpty(livenessLink))
		{
			Navigation.NavigateTo(livenessLink);
		}
	}
}
