namespace ATS.Data.DataSeed;

public class ATSInitialData
{
	private readonly ISecureToken _secureToken;
	private readonly IHashService _hashService;
	private readonly ISecretProtector _secretProtector;
	private readonly int _applicationFormExpiryInHours;
	private readonly string? _primarySenderEmail;
	private readonly string? _primaryAppPassword;
	private readonly string _primarySmtpHost;
	private readonly int _primarySmtpPort;
	private readonly int _defaultDailySendLimit;

	public ATSInitialData(
		ISecureToken secureToken,
		IHashService hashService,
		ISecretProtector secretProtector,
		IOptions<AtsEmailDeliveryOptions> emailDeliveryOptions,
		IConfiguration configuration)
	{
		_secureToken = secureToken;
		_hashService = hashService;
		_secretProtector = secretProtector;
		_applicationFormExpiryInHours = configuration
			.GetSection("ATS")
			.GetValue<int>("ATSApplicationFormExpiryInHours");

		// The pre-registry sender, read from the same keys SmtpConnectionPool used, so the
		// seeded row is the account already in production rather than a new one.
		_primarySenderEmail = configuration["Email:ATSGmail:SenderEmail"];
		_primaryAppPassword = configuration["Email:ATSGmail:AppPassword"];
		_primarySmtpHost = configuration["Email:Gmail:SmtpHost"] ?? "smtp.gmail.com";
		_primarySmtpPort = int.TryParse(configuration["Email:Gmail:SmtpPort"], out var port)
			? port
			: 587;

		_defaultDailySendLimit = emailDeliveryOptions.Value.DefaultDailySendLimit;
	}

	#region Email Invitation Request
	private const string SeedResourceName =
		"ATS.Data.DataSeed.IntouchEmailInvitationInitialData.json";

	public IReadOnlyList<EmailInvitationRequest> GetEmailInvitationRequests(
		IReadOnlyDictionary<string, Guid> userIdsByEmail,
		IReadOnlyDictionary<string, int> packageIdsByName)
	{
		var assembly = typeof(ATSInitialData).Assembly;
		using var stream = assembly.GetManifestResourceStream(SeedResourceName)
			?? throw new InvalidOperationException(
				$"Embedded ATS seed resource '{SeedResourceName}' was not found.");

		var rows = JsonSerializer.Deserialize<List<IntouchEmailInvitationSeedRow>>(stream)
			?? throw new InvalidOperationException(
				$"Embedded ATS seed resource '{SeedResourceName}' is invalid.");

		// The imported requestor is a display name, so it is matched against the seeded
		// user names to reach the auth id that GetATSUsers stores on the ATS user rows.
		var requestorIdsByName = GetUserModules()
			.Where(user => userIdsByEmail.ContainsKey(user.UserEmail))
			.ToDictionary(
				user => NormalizeRequestor(user.UserName),
				user => userIdsByEmail[user.UserEmail]);

		// Orders carry a foreign key to their package, so a seed row whose package name
		// has no matching row cannot be created at all.
		return rows
			.Where(row => packageIdsByName.ContainsKey(row.SelectPackage))
			.Select(row => CreateEmailInvitationRequest(row, requestorIdsByName, packageIdsByName))
			.ToArray();
	}

	// The imported names carry the spacing and casing of the source workbook, so they
	// are compared on a normalized form instead of the raw value.
	private static string NormalizeRequestor(string requestor) =>
		string.Join(' ', requestor.Split(
			(char[]?)null,
			StringSplitOptions.RemoveEmptyEntries))
			.ToUpperInvariant();

	private EmailInvitationRequest CreateEmailInvitationRequest(
		IntouchEmailInvitationSeedRow row,
		IReadOnlyDictionary<string, Guid> requestorIdsByName,
		IReadOnlyDictionary<string, int> packageIdsByName)
	{
		var emailAddress = $"intouch.{row.TicketNo.ToLowerInvariant()}@seed.local";
		var createdAt = row.OrderCreatedAt;
		var projectionUpdatedAt = row.OrderCompletedAt ?? createdAt;

		var hashToken = _hashService.Hash(_secureToken.GenerateSecureToken());

		// An unmatched requestor keeps the imported name and leaves the id unset so the
		// row stays identifiable instead of pointing at the wrong user.
		var requestorId = requestorIdsByName
			.TryGetValue(NormalizeRequestor(row.Requestor), out var matchedRequestorId)
				? matchedRequestorId
				: (Guid?)null;

		return new EmailInvitationRequest
		{
			EmailInvitationID = row.EmailInvitationId,
			LastName = row.LastName,
			FirstName = row.FirstName,
			EmailAddress = emailAddress,
			MobileNumber = "N/A",
			Requestor = row.Requestor,
			// Filtered above, so the lookup always succeeds.
			PackageId = packageIdsByName[row.SelectPackage],
			SelectPackage = row.SelectPackage,
			RushNormal = row.RushNormal,
			ClientId = row.ClientId,
			RequestorId = requestorId,
			HashToken = hashToken,
			HashTokenCreatedAt = createdAt,
			HashTokenExpiration = createdAt.AddHours(_applicationFormExpiryInHours),

			ApplicationFormStatus = Constants.ApplicationFormStatus.Done,
			FormCompletedAt = createdAt,
			EmailSentStatus = EmailStatus.Done,
			EmailSentAt = createdAt,
			OrderStatus = row.OrderStatus,
			OrderCreatedAt = row.OrderCreatedAt,
			OrderCompletedAt = row.OrderCompletedAt,

			NeedsProjection = false,
			ProjectionUpdatedAt = projectionUpdatedAt,
			PersonalDetails = new PersonalDetails
			{
				PersonalID = row.EmailInvitationId,
				EmailInvitationID = row.EmailInvitationId,
				FirstName = row.FirstName,
				LastName = row.LastName,
				MobileNumber = "N/A",
				EmailAddress = emailAddress,
				CreatedDate = createdAt
			},
			AddressDetails = new AddressDetails
			{
				AddressId = row.EmailInvitationId,
				EmailInvitationID = row.EmailInvitationId,
				CreatedDate = createdAt
			},
			EducationalBackground = new EducationalBackground
			{
				EducationalBackgroundID = row.EmailInvitationId,
				EmailInvitationID = row.EmailInvitationId,
				CreatedDate = createdAt
			},
			LicensesDetails = new LicensesDetails
			{
				LicensesDetailsID = row.EmailInvitationId,
				EmailInvitationID = row.EmailInvitationId,
				CreatedDate = createdAt
			},
			ProfessionalExperiences = new ProfessionalExperiences
			{
				ProfessionalExperiencesID = row.EmailInvitationId,
				EmailInvitationID = row.EmailInvitationId,
				CreatedDate = createdAt
			},
			ReferenceDetails = new ReferenceDetails
			{
				ReferenceDetailsID = row.EmailInvitationId,
				EmailInvitationID = row.EmailInvitationId,
				CreatedDate = createdAt
			},
			SignatureDetails = new SignatureDetails
			{
				SignatureDetailsID = row.EmailInvitationId,
				EmailInvitationID = row.EmailInvitationId
			},
			ReportDetails =
			[
				new ReportDetails
				{
					ReportFileId = row.EmailInvitationId,
					EmailInvitationRequestId = row.EmailInvitationId,
					HitStatus = row.HitStatus,
					ReportStatus = row.ReportStatus,
					ReportFileName = string.Empty,
					ReportFileKey = string.Empty,
					ReportUploadedAt = row.ReportUploadedAt
				}
			],
			ApplicantSearchProjection = new ApplicantSearchProjection
			{
				EmailInvitationRequestId = row.EmailInvitationId,
				FirstName = row.FirstName,
				LastName = row.LastName,
				EmailAddress = emailAddress,
				MobileNumber = "N/A",
				SelectPackage = row.SelectPackage,
				RushNormal = row.RushNormal,
				OrderStatus = row.OrderStatus,
				OrderCreatedAt = row.OrderCreatedAt,
				OrderCompletedAt = row.OrderCompletedAt,
				ApplicationFormStatus = Constants.ApplicationFormStatus.Done,
				ProjectionUpdatedAt = projectionUpdatedAt
			}
		};
	}

	private sealed class IntouchEmailInvitationSeedRow
	{
		public Guid EmailInvitationId { get; set; }
		public string TicketNo { get; set; } = string.Empty;
		public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public string SelectPackage { get; set; } = string.Empty;
		public string RushNormal { get; set; } = string.Empty;
		public int ClientId { get; set; }
		public string OrderStatus { get; set; } = string.Empty;
		public DateTime OrderCreatedAt { get; set; }
		public DateTime? OrderCompletedAt { get; set; }
		public string HitStatus { get; set; } = string.Empty;
		public string ReportStatus { get; set; } = string.Empty;
		public DateTime ReportUploadedAt { get; set; }
		public string Requestor { get; set; } = string.Empty;
	}
	#endregion

	#region USERS with Modules
	public static IEnumerable<string> GetATSUserEmails() =>
		GetUserModules().Select(user => user.UserEmail);

	public IEnumerable<UserDetails> GetATSUsers(IReadOnlyDictionary<string, Guid> userIdsByEmail)
	{
		var users = new List<UserDetails>();

		var userModules = GetUserModules();

		foreach (var user in userModules)
		{
			if (!userIdsByEmail.TryGetValue(user.UserEmail, out var userId))
			{
				continue;
			}

			foreach (var moduleId in user.ModuleId)
			{
				users.Add(new UserDetails
				{
					UserId = userId,
					UserEmail = user.UserEmail,
					UserName = user.UserName,
					RoleId = user.RoleId,
					Site = "All",
					IsActive = true,
					ModuleId = moduleId,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				});
			}
		}

		return users;
	}

	// Bulk Uploads is granted wherever New Order is: anyone who can submit a CSV must be
	// able to see what happened to it. Every seed row below already carries module 2.
	private static IEnumerable<ATSUserModuleSeedRow> GetUserModules() =>
	[
		new ATSUserModuleSeedRow(
			"atsManager@cibi.com",
			"ATS Platform Manager",
			1,
			[.. Enumerable.Range(1, 10), AtsModuleIds.AIAssistant, AtsModuleIds.BulkUploads]),

		new ATSUserModuleSeedRow(
			"atsAdmin@cibi.com",
			"ATS Admin",
			2,
			[.. Enumerable.Range(1, 10), AtsModuleIds.BulkUploads]),

		new ATSUserModuleSeedRow(
			"atsService@cibi.com",
			"ATS Service Delivery",
			3,
			[.. Enumerable.Range(1, 13)]),

		new ATSUserModuleSeedRow(
			"atsUser@cibi.com",
			"ATS User",
			3,
			[.. Enumerable.Range(1, 3), AtsModuleIds.AIAssistant, AtsModuleIds.BulkUploads]),

		new ATSUserModuleSeedRow(
			"atsUploader@cibi.com",
			"ATS Uploader",
			4,
			[.. Enumerable.Range(1, 3), AtsModuleIds.BulkUploads])
	];

	private sealed record ATSUserModuleSeedRow(
		string UserEmail,
		string UserName,
		int RoleId,
		int[] ModuleId);
	#endregion

	#region Roles
	public IEnumerable<RoleDetails> GetATSRoles()
	{
		return new List<RoleDetails>
		   {
			new RoleDetails
			{
				RoleId = 1,
				RoleName = "Platform Manager",
				RoleDescription = "Platform manager role for ATS system.",
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},

			new RoleDetails
			{
				RoleId = 2,
				RoleName = "Client Admin",
				RoleDescription = "Administrator role for ATS system.",
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},

			new RoleDetails
			{
				RoleId = 3,
				RoleName = "Service Delivery",
				RoleDescription = "Service Delivery role for ATS system.",
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},

			new RoleDetails
			{
				RoleId = 4,
				RoleName = "User",
				RoleDescription = "Basic user role for ATS system.",
				IsActive = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			}
		};

	}
	#endregion

	#region Modules
	public IEnumerable<ModuleDetails> GetATSModules() =>
	[
		new()
		   {
			   ModuleId = AtsModuleIds.Dashboard,
			   ModuleName = "Dashboard",
			   ModuleDescription = "Dashboard module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.NewOrder,
			   ModuleName = "New Order",
			   ModuleDescription = "New order module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.OrdersAndReports,
			   ModuleName = "Orders & Reports",
			   ModuleDescription = "Orders and reports module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.Disputes,
			   ModuleName = "Disputes",
			   ModuleDescription = "Disputes module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.Withdrawn,
			   ModuleName = "Withdrawn",
			   ModuleDescription = "Withdrawn module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.PackageManagement,
			   ModuleName = "Package Management",
			   ModuleDescription = "Package management module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.ClientManagement,
			   ModuleName = "Client Management",
			   ModuleDescription = "Client management module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.RoleManagement,
			   ModuleName = "Role Management",
			   ModuleDescription = "Role management module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.ModuleManagement,
			   ModuleName = "Module Management",
			   ModuleDescription = "Module management module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.UserManagement,
			   ModuleName = "User Management",
			   ModuleDescription = "User management module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.ClientAssigning,
			   ModuleName = "Client Assigning",
			   ModuleDescription = "Client assigning module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.AIAssistant,
			   ModuleName = "AI Assistant",
			   ModuleDescription = "AI assistant module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.BulkUploads,
			   ModuleName = "Bulk Uploads",
			   ModuleDescription = "Bulk upload monitoring module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.TicketingStatus,
			   ModuleName = "Ticketing Status",
			   ModuleDescription = "OMS auto-ticketing monitoring module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.AuditTrail,
			   ModuleName = "Audit Trail",
			   ModuleDescription = "User action audit trail module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   },
		   new()
		   {
			   ModuleId = AtsModuleIds.EmailAccountManagement,
			   ModuleName = "Email Accounts",
			   ModuleDescription = "Sender email account management module for ATS system.",
			   IsActive = true,
			   CreatedAt = DateTime.UtcNow,
			   UpdatedAt = DateTime.UtcNow
		   }
	];
	#endregion

	#region Sender email account
	/// <summary>
	/// The sender account the queue used before accounts were rows: the one configured in
	/// <c>Email:ATSGmail</c>. Returns null when that configuration is absent.
	/// </summary>
	/// <remarks>
	/// Seeded so the migration is deployable on its own. Without it the table lands empty, the
	/// selector finds no sendable account, and every queued invitation defers until somebody
	/// registers one through the UI - a silent outage caused by shipping the schema.
	///
	/// Seeded as <c>Verified</c> without sending a code, which is the one place that status is
	/// granted unproven. It is justified because these exact credentials are what the queue has
	/// been sending through in production; requiring a code here would retire a working sender
	/// to prove something its own delivery history already has.
	/// </remarks>
	public AtsEmailAccount? GetPrimaryEmailAccount()
	{
		if (string.IsNullOrWhiteSpace(_primarySenderEmail)
			|| string.IsNullOrWhiteSpace(_primaryAppPassword))
		{
			return null;
		}

		var now = DateTime.UtcNow;

		return new AtsEmailAccount
		{
			DisplayName = "CIBI Recruitment",
			EmailAddress = _primarySenderEmail,
			SmtpHost = _primarySmtpHost,
			SmtpPort = _primarySmtpPort,
			EncryptedPassword = _secretProtector.Protect(
				_primaryAppPassword,
				AtsEmailAccountSecrets.PasswordContext(_primarySenderEmail)),

			// Priority 1 so the seeded account keeps carrying the traffic it already carries;
			// an account registered later takes 2 and only sees messages once this one is
			// capped or unhealthy.
			Priority = 1,
			IsActive = true,
			DailySendLimit = _defaultDailySendLimit,
			VerificationStatus = AtsEmailAccountStatus.Verified,
			VerifiedAt = now,
			ConsecutiveFailureCount = 0,
			CreatedAt = now,
			UpdatedAt = now
		};
	}
	#endregion
}
