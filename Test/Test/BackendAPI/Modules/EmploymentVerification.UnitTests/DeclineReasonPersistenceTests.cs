using ATS.Shared.Contracts;
using BuildingBlocks.SharedServices.Interfaces;
using EmploymentVerification.Data.Entities;
using EmploymentVerification.Data.Repository;
using EmploymentVerification.DTO;
using EmploymentVerification.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// A reason belongs to the rejection it was typed against. These pin all three writers
/// of the terminal response: a decline stores the reason, a confirmation clears it, and
/// a send that could not be delivered stores nothing - so no row ever carries a
/// sentence explaining an inaccuracy nobody reported.
/// </summary>
public class DeclineReasonPersistenceTests
{
	private const string Token = "stored-token-hash";

	private readonly Mock<IEmploymentVerificationRepository> _repository = new(MockBehavior.Strict);

	private VerificationRequestStatus? _capturedStatus;
	private string? _capturedNotes;

	private static IConfiguration EmptyConfiguration() =>
		new ConfigurationBuilder()
			.AddInMemoryCollection()
			.Build();

	private EmploymentVerificationService CreateSut() =>
		new(
			_repository.Object,
			Mock.Of<IATSVerificationDataProvider>(),
			Mock.Of<IEmailService>(),
			Mock.Of<IHashService>(),
			EmptyConfiguration());

	private static EmploymentVerificationRequest SentRequest() =>
		new()
		{
			Id = Guid.NewGuid(),
			CandidateName = "Juan Dela Cruz",
			PreviousEmployer = "CONCENTRIX",
			Position = "Analyst",
			HrEmail = "hr@concentrix.test",
			Status = VerificationRequestStatus.Sent,
			RequestedAt = DateTime.UtcNow.AddDays(-1),
			SentAt = DateTime.UtcNow.AddDays(-1),
			TokenExpiresAt = DateTime.UtcNow.AddHours(24),
			VerificationTokenHash = Token
		};

	private void ExpectResponse(EmploymentVerificationRequest request)
	{
		_repository
			.Setup(repository => repository.FindByTokenHashAsync(
				Token,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(request);

		_repository
			.Setup(repository => repository.MarkRespondedAsync(
				request.Id,
				It.IsAny<VerificationRequestStatus>(),
				It.IsAny<DateTime>(),
				It.IsAny<string?>(),
				It.IsAny<CancellationToken>()))
			.Callback<Guid, VerificationRequestStatus, DateTime, string?, CancellationToken>(
				(_, status, _, notes, _) =>
				{
					_capturedStatus = status;
					_capturedNotes = notes;
				})
			.ReturnsAsync(true);
	}

	[Fact]
	public async Task VerifyAsync_ShouldStoreTheTrimmedReason_WhenTheContactDeclines()
	{
		var request = SentRequest();
		ExpectResponse(request);

		var result = await CreateSut().VerifyAsync(
			Token,
			reject: true,
			"  The end date is a year out.  ",
			CancellationToken.None);

		result.Status.Should().Be(CompletionStatus.Completed);
		_capturedStatus.Should().Be(VerificationRequestStatus.Rejected);
		_capturedNotes.Should().Be("The end date is a year out.");
	}

	[Fact]
	public async Task VerifyAsync_ShouldStoreNoReason_WhenTheContactConfirms()
	{
		var request = SentRequest();
		ExpectResponse(request);

		var result = await CreateSut().VerifyAsync(
			Token,
			reject: false,
			"text a previous decline attempt left behind",
			CancellationToken.None);

		result.Status.Should().Be(CompletionStatus.Completed);
		_capturedStatus.Should().Be(VerificationRequestStatus.Verified);
		_capturedNotes.Should().BeNull();
	}

	[Fact]
	public async Task CreateAndSendAsync_ShouldStoreNoReason_WhenTheEmailCannotBeSent()
	{
		// The one terminal write with no human behind it: the send failed, so the row is
		// marked Expired to release its segment. The strict mock is configured with a
		// literal null for the notes argument, so any reason reaching it leaves the call
		// unmatched and fails the test.
		var hashService = new Mock<IHashService>();
		hashService
			.Setup(service => service.Hash(It.IsAny<string>()))
			.Returns(Token);

		var emailService = new Mock<IEmailService>();
		emailService
			.Setup(service => service.SendEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<bool>()))
			.ReturnsAsync(false);

		var repository = new Mock<IEmploymentVerificationRepository>(MockBehavior.Strict);
		repository
			.Setup(repo => repo.AddAsync(
				It.IsAny<EmploymentVerificationRequest>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);
		repository
			.Setup(repo => repo.MarkRespondedAsync(
				It.IsAny<Guid>(),
				VerificationRequestStatus.Expired,
				It.IsAny<DateTime>(),
				null,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		var sut = new EmploymentVerificationService(
			repository.Object,
			Mock.Of<IATSVerificationDataProvider>(),
			emailService.Object,
			hashService.Object,
			EmptyConfiguration());

		var act = () => sut.CreateAndSendAsync(
			new CreateEmploymentVerificationRequest(
				"Juan Dela Cruz",
				"CONCENTRIX",
				"Analyst",
				"hr@concentrix.test",
				null,
				null,
				Guid.NewGuid(),
				1,
				"Directory"),
			CancellationToken.None);

		await act.Should().ThrowAsync<InvalidOperationException>();

		repository.Verify(
			repo => repo.MarkRespondedAsync(
				It.IsAny<Guid>(),
				VerificationRequestStatus.Expired,
				It.IsAny<DateTime>(),
				null,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
