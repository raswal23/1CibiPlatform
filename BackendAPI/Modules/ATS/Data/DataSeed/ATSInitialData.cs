using System.Text.Json;

namespace ATS.Data.DataSeed;

public class ATSInitialData
{
	private const string SeedResourceName =
		"ATS.Data.DataSeed.IntouchEmailInvitationInitialData.json";

	public IReadOnlyList<EmailInvitationRequest> GetEmailInvitationRequests()
	{
		var assembly = typeof(ATSInitialData).Assembly;
		using var stream = assembly.GetManifestResourceStream(SeedResourceName)
			?? throw new InvalidOperationException(
				$"Embedded ATS seed resource '{SeedResourceName}' was not found.");

		var rows = JsonSerializer.Deserialize<List<IntouchEmailInvitationSeedRow>>(stream)
			?? throw new InvalidOperationException(
				$"Embedded ATS seed resource '{SeedResourceName}' is invalid.");

		return rows.Select(CreateEmailInvitationRequest).ToArray();
	}

	private static EmailInvitationRequest CreateEmailInvitationRequest(
		IntouchEmailInvitationSeedRow row)
	{
		var emailAddress = $"intouch.{row.TicketNo.ToLowerInvariant()}@seed.local";
		var createdAt = row.OrderCreatedAt;
		var projectionUpdatedAt = row.OrderCompletedAt ?? createdAt;

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
			HashToken = row.TicketNo,
			ApplicationFormStatus = Constants.ApplicationFormStatus.Done,
			FormCompletedAt = createdAt,
			EmailSentStatus = EmailStatus.Done,
			EmailSentAt = createdAt,
			HashTokenCreatedAt = createdAt,
			HashTokenExpiration = createdAt.AddDays(7),
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
		public string OrderStatus { get; set; } = string.Empty;
		public DateTime OrderCreatedAt { get; set; }
		public DateTime? OrderCompletedAt { get; set; }
		public string HitStatus { get; set; } = string.Empty;
		public string ReportStatus { get; set; } = string.Empty;
		public DateTime ReportUploadedAt { get; set; }
		public string Requestor { get; set; } = string.Empty;
	}
}
