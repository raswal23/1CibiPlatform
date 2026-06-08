namespace BuildingBlocks.SharedServices.Interfaces;

public interface IEmailService
{
	Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);

	string SendOtpBody(string name, string otpCode);

	string SendPasswordResetBody(string name, string resetLink, int expireMins);

	string SendNotificationBody(string gmail, string application, string submenu, string role);

	string SendApprovalNotificationBody(string gmail);

	string SendAppplicationFormNotification(string gmail, string name, string applicationFormLink);

}
