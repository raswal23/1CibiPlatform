namespace ATS.Services.EmailService;

public class ATSEmailService : IEmailService
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<ATSEmailService> _logger;
	private readonly int _atsApplicationFormExpirationInHours;
	private readonly string _senderEmail;
	private readonly string _appPassword;
	private readonly string _smtpHost;
	private readonly int _smtpPort;

	public ATSEmailService(IConfiguration configuration, ILogger<ATSEmailService> logger)
	{
		_configuration = configuration;
		_logger = logger;
		_senderEmail = _configuration["Email:ATSGmail:SenderEmail"]
			?? throw new InvalidOperationException("Email:Gmail:SenderEmail not configured");
		_appPassword = _configuration["Email:ATSGmail:AppPassword"]
			?? throw new InvalidOperationException("Email:Gmail:AppPassword not configured");
		_smtpHost = _configuration["Email:Gmail:SmtpHost"] ?? "smtp.gmail.com";
		_smtpPort = int.Parse(_configuration["Email:Gmail:SmtpPort"] ?? "587");
		_atsApplicationFormExpirationInHours = _configuration.GetSection("ATS").GetValue<int>("ATSApplicationFormExpiryInHours");
	}
	public async Task<bool> SendATSEmailAsync(string toEmail, string subject, string body)
	{
		try
		{
			using (var smtpClient = new SmtpClient(_smtpHost, _smtpPort))
			{
				// Gmail requires TLS
				smtpClient.EnableSsl = true;
				smtpClient.UseDefaultCredentials = false;
				smtpClient.Credentials = new NetworkCredential(_senderEmail, _appPassword);
				smtpClient.Timeout = 10000; 

				using (var mailMessage = new MailMessage())
				{
					mailMessage.From = new MailAddress(_senderEmail, "WFS - ATS");
					mailMessage.To.Add(toEmail);
					mailMessage.Subject = subject;
					mailMessage.Body = body;
					mailMessage.IsBodyHtml = true;

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

	public string SendAppplicationFormNotification(string gmail, string name, string applicationFormLink)
	{
		string body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; border: 1px solid gray; border-radius: 4px; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px;}}
                        .header {{ background: linear-gradient(90deg,#102247 0%,#2a77ae 50%,#68c0d6 100%); color: white; padding: 20px; text-align: center; border-radius: 4px;}}
                        .content {{ padding: 20px; background-color: #f9f9f9; }}
                        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>CIBI | Background Verification Information Request</h1>
                        </div>
                        <div class='content'>
                            <p>Dear {name},</p>
							<p>
								Princess Espiritu, TaskUs has requested CIBI Information Inc. to perform background checks on you as part of their pre-employment screening process. Please sign up on the link provided: 
								<a href='{applicationFormLink}'>Application Form</a> 
							</p>
							<p>Please comply <strong>within the next {_atsApplicationFormExpirationInHours} hours upon receipt of this email</strong> so we can move forward with the completion of verification.</p>
						    <p><strong>REMINDERS IN ANSWERING THE FORM </strong></p>
							<ol>
								<li>In case you do not have a SSS or TIN Number, kindly input random digits from 0 to 9 to proceed with the application.</li>
								<li>In case you have a portion to input the Email Address of HR POC, kindly input your HR person of contact on the company you are applying to.</li>
							</ol>
                            <p>
								For any questions or concerns, please do not hesitate to reach out to
								<a href=""mailto:pre-workteam@cibi.com.ph"">pre-workteam@cibi.com.ph</a>
								and
								<a href=""mailto:ceteam@cibi.com.ph"">ceteam@cibi.com.ph</a>
								or call us at +63 923 087 8757 (Sun), or +63 917 632 0486 (Globe).
							</p>
						</div>
						<div class='footer'>
							<p>This e-mail and its attachments may contain sensitive and confidential information. Do not resend, copy, or use this email if you are not the intended recipient. Please contact the sender immediately and delete this entire email. The privilege is not waived because it was delivered to you mistakenly. CIBI Information Inc. and its affiliates accept no liability for any loss or harm resulting from this e-mail and reserve the right to monitor, retain, and/or review email. The opinions stated in this email are solely those of the author and may not reflect the views of CIBI Information Inc. or its affiliates.</p>
						</div>
					</div>
				</body>
				</html>";

		return body;
	}

	public string SendApprovalNotificationBody(string gmail)
	{
		throw new NotImplementedException();
	}

	public string SendNotificationBody(string gmail, string application, string submenu, string role)
	{
		throw new NotImplementedException();
	}

	public string SendOtpBody(string name, string otpCode)
	{
		throw new NotImplementedException();
	}

	public string SendPasswordResetBody(string name, string resetLink, int expireMins)
	{
		throw new NotImplementedException();
	}

	public Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
	{
		throw new NotImplementedException();
	}
}
