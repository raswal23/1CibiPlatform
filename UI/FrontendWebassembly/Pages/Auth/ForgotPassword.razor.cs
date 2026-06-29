namespace FrontendWebassembly.Pages.Auth;

public partial class ForgotPassword
{
	private MudForm form;
	private bool formValid;
	private bool isLoading = false;
	private bool isButtonLoading = false;
	private bool isUserValid = true;
	private bool isSuccess = false;
	private string email = "";
	private string errorMessage = "";
	private string successMessage = "";

	private async Task HandleForgotPassword()
	{
		isButtonLoading = true;
		isUserValid = true;
		isSuccess = false;
		errorMessage = "";
		successMessage = "";
		StateHasChanged();
		try
		{
			var user = await IAuthService.ForgotPasswordSendEmail(new SendEmailForgotPasswordRequestDTO(email));
			if (!string.IsNullOrEmpty(user.errorMessage))
			{
				isUserValid = false;
				errorMessage = user.errorMessage;
				isButtonLoading = false;
				StateHasChanged();
				return;
			}

			isSuccess = true;
			successMessage = "A reset link has been sent to your email.";
		}
		catch (Exception ex)
		{
			isUserValid = false;
			Console.WriteLine(ex);
			errorMessage = "An unexpected error occurred. Please try again.";
		}
		isButtonLoading = false;
		StateHasChanged();
	}
}
