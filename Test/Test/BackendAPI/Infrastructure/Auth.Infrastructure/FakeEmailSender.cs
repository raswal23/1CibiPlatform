using BuildingBlocks.SharedServices.Interfaces;
namespace Test.BackendAPI.Infrastructure.Auth.Infrastructure;

public class FakeEmailSender : IEmailService
{
	public Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
		=> Task.FromResult(true);

	public string SendOtpBody(string name, string otpCode)
		=> $"Hello {name}, your OTP code is {otpCode}";

	public string SendPasswordResetBody(string name, string resetLink, int expireMins)
		=> $"Hello {name}, to reset your password, please use the following link: {resetLink}. This link will expire in {expireMins} minutes.";

	public string SendNotificationBody(string gmail, string application, string submenu, string role)
		=> $"Hello {gmail}, you have been granted access to {application} — {submenu} with the role of {role}.";

	public string SendApprovalNotificationBody(string gmail)
		=> $"Hello {gmail}, your account has been approved.";

	public string SendAppplicationFormNotification(string gmail, string name, string applicationFormLink)
		=> $"Hello {name}/{gmail}, please complete your application form using this link: {applicationFormLink}";
}
