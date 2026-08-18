namespace ATS.Data.DataSeed;

public class ATSInitialData
{
	private readonly ISecureToken _secureToken;
	private readonly IHashService _hashService;
	private readonly int _applicationFormExpiryInHours;

	public ATSInitialData(
		ISecureToken secureToken,
		IHashService hashService,
		IConfiguration configuration)
	{
		_secureToken = secureToken;
		_hashService = hashService;
		_applicationFormExpiryInHours = configuration
			.GetSection("ATS")
			.GetValue<int>("ATSApplicationFormExpiryInHours");
	}

	#region Email Invitation Request
	private const string SeedResourceName =
		"ATS.Data.DataSeed.IntouchEmailInvitationInitialData.json";

	public IReadOnlyList<EmailInvitationRequest> GetEmailInvitationRequests(
		IReadOnlyDictionary<string, Guid> userIdsByEmail)
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

		return rows
			.Select(row => CreateEmailInvitationRequest(row, requestorIdsByName))
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
		IReadOnlyDictionary<string, Guid> requestorIdsByName)
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

	private static IEnumerable<ATSUserModuleSeedRow> GetUserModules() =>
	[
		new ATSUserModuleSeedRow(
			"atsManager@cibi.com",
			"ATS Platform Manager",
			1,
			[.. Enumerable.Range(1, 10), AtsModuleIds.AIAssistant]),

		new ATSUserModuleSeedRow(
			"atsAdmin@cibi.com",
			"ATS Admin",
			2,
			Enumerable.Range(1, 10).ToArray()),

		new ATSUserModuleSeedRow(
			"atsService@cibi.com",
			"ATS Service Delivery",
			3,
			[3]),

		new ATSUserModuleSeedRow(
			"atsUser@cibi.com",
			"ATS User",
			3,
			[.. Enumerable.Range(1, 3), AtsModuleIds.AIAssistant]),

		new ATSUserModuleSeedRow(
			"atsUploader@cibi.com",
			"ATS Uploader",
			4,
			Enumerable.Range(1, 3).ToArray())
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
}
