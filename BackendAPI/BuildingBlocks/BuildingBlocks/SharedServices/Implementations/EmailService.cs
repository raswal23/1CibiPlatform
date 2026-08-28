

namespace BuildingBlocks.SharedServices.Implementations;

public class EmailService : IEmailService
{

	private readonly IConfiguration _configuration;
	private readonly ILogger<EmailService> _logger;
	private readonly string _senderEmail;
	private readonly string _appPassword;
	private readonly string _smtpHost;
	private readonly int _smtpPort;
	private readonly int _expirationInMinutes;
	private readonly string _onePlatformLink;

	public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
	{
		_configuration = configuration;
		_logger = logger;

		// Load from appsettings.json
		_senderEmail = _configuration["Email:Gmail:SenderEmail"]
			?? throw new InvalidOperationException("Email:Gmail:SenderEmail not configured");
		_appPassword = _configuration["Email:Gmail:AppPassword"]
			?? throw new InvalidOperationException("Email:Gmail:AppPassword not configured");
		_smtpHost = _configuration["Email:Gmail:SmtpHost"] ?? "smtp.gmail.com";
		_smtpPort = int.Parse(_configuration["Email:Gmail:SmtpPort"] ?? "587");
		_expirationInMinutes = int.Parse(_configuration["Email:OtpExpirationInMinutes"] ?? "15");
		_onePlatformLink = _configuration["PhilSys:LivenessBaseUrl"]
			?? throw new InvalidOperationException("OnePlatformLink not configured"); ;

	}

	public async Task<bool> SendEmailAsync(
		string toEmail,
		string subject,
		string body,
		bool isHtml = true)
	{
		try
		{
			using (var smtpClient = new SmtpClient(_smtpHost, _smtpPort))
			{
				// Gmail requires TLS
				smtpClient.EnableSsl = true;
				smtpClient.UseDefaultCredentials = false;
				smtpClient.Credentials = new NetworkCredential(_senderEmail, _appPassword);
				smtpClient.Timeout = 10000; // 10 seconds timeout

				using (var mailMessage = new MailMessage())
				{
					mailMessage.From = new MailAddress(_senderEmail, "NoSent Auth");
					mailMessage.To.Add(toEmail);
					mailMessage.Subject = subject;
					mailMessage.Body = body;
					mailMessage.IsBodyHtml = isHtml;

					await smtpClient.SendMailAsync(mailMessage);

					_logger.LogInformation($"Email sent successfully to {toEmail}");
					return true;
				}
			}
		}
		catch (SmtpException ex)
		{
			_logger.LogError($"SMTP Error sending email to {toEmail}: {ex.Message}");
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error sending email to {toEmail}: {ex.Message}");
			return false;
		}
	}


	public string SendOtpBody(
		string name,
		string otpCode)
	{
		string body = $@"
			<!DOCTYPE html>
			<html>
			<body style='margin:0;padding:0;background:#f4f6fb;font-family:Arial, sans-serif'>
				<div style='max-width:600px;margin:24px auto;background:#ffffff;border:1px solid #d9e5f5;border-radius:12px;overflow:hidden'>
					<div style='padding:24px 36px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-align:center'>
						<h1 style='margin:0;font-size:20px'>Email Verification</h1>
						<p style='margin:8px 0 0;font-size:13px;line-height:1.5;color:#dbe7fb'>Confirm your email address to finish setting up your account</p>
					</div>
					<div style='padding:34px 36px'>
						<p style='font-size:16px;line-height:1.7;text-align:center'>Hello {name},</p>
						<p style='font-size:16px;line-height:1.7;text-align:center'>Thank you for registering with us. Please use the following code to verify your email address:</p>
						<div style='background:#f4f8fd;border:2px solid #1d5fd1;border-radius:12px;padding:20px;text-align:center;font-size:32px;font-weight:bold;letter-spacing:5px;margin:24px 0'>{otpCode}</div>
						<p style='font-size:15px;line-height:1.6;text-align:center'>This code will expire in {_expirationInMinutes} minutes.</p>
						<p style='font-size:12px;line-height:1.6;color:#6b7c92;text-align:center'>If you did not create this account, please ignore this email.</p>
					</div>
					<div style='padding:20px 36px;background:#f4f8fd;color:#66788f;font-size:12px;line-height:1.6;text-align:center'>&copy; 2026 NoSent. All rights reserved.</div>
				</div>
			</body>
			</html>";

		return body;
	}

	/// <summary>
	/// Send password reset email
	/// </summary>
	public string SendPasswordResetBody(
		string name,
		string resetLink,
		int expireMins)
	{
		string body = $@"
			<!DOCTYPE html>
			<html>
			<body style='margin:0;padding:0;background:#f4f6fb;font-family:Arial, sans-serif'>
				<div style='max-width:600px;margin:24px auto;background:#ffffff;border:1px solid #d9e5f5;border-radius:12px;overflow:hidden'>
					<div style='padding:24px 36px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-align:center'>
						<h1 style='margin:0;font-size:20px'>Password Reset</h1>
						<p style='margin:8px 0 0;font-size:13px;line-height:1.5;color:#dbe7fb'>We received a request to reset the password for your account</p>
					</div>
					<div style='padding:34px 36px'>
						<p style='font-size:16px;line-height:1.7;text-align:center'>Hello {name},</p>
						<p style='font-size:16px;line-height:1.7;text-align:center'>We received a request to reset your password. Click the button below to reset it:</p>
						<p style='margin:28px 0 12px;text-align:center'><a href='{resetLink}' style='display:inline-block;padding:14px 26px;border-radius:999px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-decoration:none;font-weight:bold'>Reset Password</a></p>
						<p style='font-size:15px;line-height:1.6;text-align:center'>This link will expire in {expireMins} minutes.</p>
						<p style='font-size:12px;line-height:1.6;color:#6b7c92;text-align:center'>If you did not request this, please ignore this email.</p>
					</div>
					<div style='padding:20px 36px;background:#f4f8fd;color:#66788f;font-size:12px;line-height:1.6;text-align:center'>&copy; 2026 NoSent. All rights reserved.</div>
				</div>
			</body>
			</html>";

		return body;
	}

	public string SendNotificationBody(
		string gmail,
		string application,
		string submenu,
		string role
		)
	{
		string body = $@"
			<!DOCTYPE html>
			<html>
			<body style='margin:0;padding:0;background:#f4f6fb;font-family:Arial, sans-serif'>
				<div style='max-width:600px;margin:24px auto;background:#ffffff;border:1px solid #d9e5f5;border-radius:12px;overflow:hidden'>
					<div style='padding:24px 36px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-align:center'>
						<h1 style='margin:0;font-size:20px'>OnePlatform Account Assigned</h1>
						<p style='margin:8px 0 0;font-size:13px;line-height:1.5;color:#dbe7fb'>Your application access and role details in OnePlatform are ready</p>
					</div>
					<div style='padding:34px 36px'>
						<p style='font-size:16px;line-height:1.7'>Hello {gmail},</p>
						<p style='font-size:16px;line-height:1.7'>Your account has been successfully assigned the following in OnePlatform:</p>
						<table role='presentation' style='width:100%;border-collapse:collapse;margin:24px 0;background:#f4f8fd;border:1px solid #d9e5f5;border-radius:12px'>
							<tr><td style='padding:12px 16px;color:#5b6f8f;font-size:13px'>Application</td><td style='padding:12px 16px;font-weight:bold'>{application}</td></tr>
							<tr><td style='padding:12px 16px;color:#5b6f8f;font-size:13px'>Submenu</td><td style='padding:12px 16px;font-weight:bold'>{submenu}</td></tr>
							<tr><td style='padding:12px 16px;color:#5b6f8f;font-size:13px'>Role</td><td style='padding:12px 16px;font-weight:bold'>{role}</td></tr>
						</table>
						<p style='font-size:15px;line-height:1.6'>You can now access the assigned application and perform tasks according to your role.</p>
						<p style='margin:28px 0 12px;text-align:center'><a href='{_onePlatformLink}' style='display:inline-block;padding:14px 26px;border-radius:999px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-decoration:none;font-weight:bold'>Go to OnePlatform</a></p>
						<p style='font-size:12px;line-height:1.6;color:#6b7c92;text-align:center'>If you did not expect this assignment, please contact your administrator immediately.</p>
					</div>
					<div style='padding:20px 36px;background:#f4f8fd;color:#66788f;font-size:12px;line-height:1.6;text-align:center'>&copy; 2026 NoSent. All rights reserved.</div>
				</div>
			</body>
			</html>";

		return body;
	}

	public string SendApprovalNotificationBody(string gmail)
	{
		string body = $@"
			<!DOCTYPE html>
			<html>
			<body style='margin:0;padding:0;background:#f4f6fb;font-family:Arial, sans-serif'>
				<div style='max-width:600px;margin:24px auto;background:#ffffff;border:1px solid #d9e5f5;border-radius:12px;overflow:hidden'>
					<div style='padding:24px 36px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-align:center'>
						<h1 style='margin:0;font-size:20px'>OnePlatform Account Assigned</h1>
						<p style='margin:8px 0 0;font-size:13px;line-height:1.5;color:#dbe7fb'>Your account has been approved and is now ready to use</p>
					</div>
					<div style='padding:34px 36px'>
						<p style='font-size:16px;line-height:1.7;text-align:center'>Hello {gmail},</p>
						<p style='font-size:16px;line-height:1.7;text-align:center'>Your account has been successfully approved.</p>
						<p style='font-size:15px;line-height:1.6;text-align:center'>You can now access the approved account.</p>
						<p style='margin:28px 0 12px;text-align:center'><a href='{_onePlatformLink}' style='display:inline-block;padding:14px 26px;border-radius:999px;background:linear-gradient(100deg, #0b1b3d 0%, #1c3a70 35%, #1d5fd1 75%, #4f93ea 100%);color:#ffffff;text-decoration:none;font-weight:bold'>Go to OnePlatform</a></p>
					</div>
					<div style='padding:20px 36px;background:#f4f8fd;color:#66788f;font-size:12px;line-height:1.6;text-align:center'>&copy; 2026 NoSent. All rights reserved.</div>
				</div>
			</body>
			</html>";

		return body;
	}

	public string SendAppplicationFormNotification(string gmail, string name, string applicationFormLink, string? requestor, string? clientName)
	{
		throw new NotImplementedException();
	}

	public Task<bool> SendATSEmailAsync(string toEmail, string subject, string body)
	{
		throw new NotImplementedException();
	}

	public string SendEmailForDispute(string gmail)
	{
		throw new NotImplementedException();
	}

	public string SendEmailForDispute(string gmail, string company, string disputeReason, DateTime? orderedAt, string requestor, string SubjectName)
	{
		throw new NotImplementedException();
	}
}

