using System.Text.Json;
using EmploymentVerification.Data.Entities;
using EmploymentVerification.DTO;
using FluentAssertions;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// Guards the invariant that the verification token hash never reaches an API response.
/// The hash is the credential embedded in the emailed link, so a caller who can read it
/// can construct a working verification link for that request.
/// </summary>
public class VerificationTokenHashExposureTests
{
	private const string TokenHash = "token-hash-that-must-never-leave-the-server";

	private static EmploymentVerificationRequest Entity() =>
		new()
		{
			Id = Guid.NewGuid(),
			AtsSubjectId = Guid.NewGuid(),
			CandidateName = "Juan Dela Cruz",
			PreviousEmployer = "Acme Corporation",
			Position = "Analyst",
			HrEmail = "hr@acme.test",
			RequestedAt = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
			TokenExpiresAt = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
			VerificationTokenHash = TokenHash
		};

	[Fact]
	public void EntityCarriesTheTokenHash_SoReturningItWouldLeakTheCredential()
	{
		// Pins the reason this projection exists. If the hash ever moves off the entity
		// these tests stop guarding anything and should be revisited.
		Entity().VerificationTokenHash.Should().Be(TokenHash);
	}

	[Fact]
	public void FromEntity_OmitsTheTokenHash()
	{
		var dto = SentVerificationRequestDTO.FromEntity(Entity());

		var json = JsonSerializer.Serialize(dto);

		json.Should().NotContain(TokenHash);
		json.Should().NotContain("verificationTokenHash");
		json.Should().NotContain("VerificationTokenHash");
	}

	[Fact]
	public void FromEntity_KeepsTheFieldsTheTrackingViewRenders()
	{
		var entity = Entity();

		var dto = SentVerificationRequestDTO.FromEntity(entity);

		dto.RequestId.Should().Be(entity.Id);
		dto.SubjectId.Should().Be(entity.AtsSubjectId);
		dto.CandidateName.Should().Be(entity.CandidateName);
		dto.PreviousEmployer.Should().Be(entity.PreviousEmployer);
		dto.Position.Should().Be(entity.Position);
		dto.HrEmail.Should().Be(entity.HrEmail);
		dto.TokenExpiresAt.Should().Be(entity.TokenExpiresAt);
	}

	[Fact]
	public void ProjectionContract_ExposesNoCredentialMember()
	{
		// The create and list slices both return this projection rather than the entity.
		// A member added here would be serialised to every caller, so the shape is pinned.
		// TokenExpiresAt is expected and safe - it is a timestamp the tracking view shows.
		var members = typeof(SentVerificationRequestDTO)
			.GetProperties()
			.Select(property => property.Name)
			.ToList();

		members.Should().NotContain("VerificationTokenHash");
		members.Should().NotContain(name =>
			name.Contains("Hash", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Token", StringComparison.OrdinalIgnoreCase)
				&& !name.Equals(nameof(SentVerificationRequestDTO.TokenExpiresAt), StringComparison.Ordinal));
	}
}
