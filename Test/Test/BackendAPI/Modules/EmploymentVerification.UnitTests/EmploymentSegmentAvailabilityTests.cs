using ATS.Shared.Contracts;
using BuildingBlocks.SharedServices.Interfaces;
using EmploymentVerification.Data.Entities;
using EmploymentVerification.Data.Repository;
using EmploymentVerification.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// Guards the invariant the segment discriminator exists for: a verification request
/// raised for one of a candidate's former employers must not suppress the others.
/// </summary>
/// <remarks>
/// All three employers of one application form share an AtsSubjectId, because the form
/// stores them as Emp1*/Emp2*/Emp3* column groups on a single row. Filtering
/// availability by subject alone - which is what this module did before - meant the
/// first employer contacted was the only one ever contacted.
/// </remarks>
public class EmploymentSegmentAvailabilityTests
{
	private static readonly Guid SubjectId = Guid.NewGuid();

	private readonly Mock<IEmploymentVerificationRepository> _repository = new(MockBehavior.Strict);
	private readonly Mock<IATSVerificationDataProvider> _atsProvider = new(MockBehavior.Strict);

	private EmploymentVerificationService CreateSut() =>
		new(
			_repository.Object,
			_atsProvider.Object,
			Mock.Of<IEmailService>(),
			Mock.Of<IHashService>(),
			new ConfigurationBuilder().AddInMemoryCollection().Build());

	private static ATSInProgressEmploymentRecord Segment(short segment, string employer) =>
		new(
			SubjectId: SubjectId,
			EmploymentSegment: segment,
			CandidateName: "Juan Dela Cruz",
			Employer: employer,
			Position: "Analyst",
			StartDate: null,
			EndDate: null,
			SupervisorName: null,
			SupervisorEmail: "supervisor@example.test",
			PermissionToContact: true);

	private void ExpectAtsSegments() =>
		_atsProvider
			.Setup(provider => provider.GetInProgressEmploymentAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new[]
			{
				Segment(1, "CONCENTRIX"),
				Segment(2, "ALORICA"),
				Segment(3, "TELEPERFORMANCE")
			});

	private void ExpectBlocked(params short[] blockedSegments) =>
		_repository
			.Setup(repository => repository.ListBlockedSegmentsAsync(
				It.IsAny<DateTime>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(blockedSegments
				.Select(segment => new BlockedEmploymentSegment(SubjectId, segment))
				.ToList());

	[Fact]
	public async Task GetAvailableATSRecordsAsync_ShouldLeaveTheOtherEmployersAvailable_WhenOneSegmentIsAlreadySent()
	{
		ExpectAtsSegments();
		ExpectBlocked(1);

		var available = await CreateSut().GetAvailableATSRecordsAsync(CancellationToken.None);

		// The regression this whole design exists to prevent. Before the discriminator
		// the blocked subject removed all three.
		available.Select(record => record.EmploymentSegment)
			.Should().BeEquivalentTo(new short[] { 2, 3 });
	}

	[Fact]
	public async Task GetAvailableATSRecordsAsync_ShouldReturnNothing_WhenEverySegmentIsBlocked()
	{
		ExpectAtsSegments();
		ExpectBlocked(1, 2, 3);

		var available = await CreateSut().GetAvailableATSRecordsAsync(CancellationToken.None);

		available.Should().BeEmpty();
	}

	[Fact]
	public async Task GetAvailableATSRecordsAsync_ShouldReturnEverySegment_WhenNothingIsBlocked()
	{
		ExpectAtsSegments();
		ExpectBlocked();

		var available = await CreateSut().GetAvailableATSRecordsAsync(CancellationToken.None);

		available.Should().HaveCount(3);
	}

	[Fact]
	public async Task GetAvailableATSRecordsAsync_ShouldNotOfferASegment_WhenTheEmployerAlreadyDeclined()
	{
		ExpectAtsSegments();
		ExpectBlocked(2);

		var available = await CreateSut().GetAvailableATSRecordsAsync(CancellationToken.None);

		// A Rejected request means the employer answered "not accurate". Offering that
		// segment again would have the job re-mail them within five minutes, which is
		// the one outcome a verification flow must never produce. Rejected used to
		// release the segment, which was defensible only while a human chose to retry.
		available.Select(record => record.EmploymentSegment)
			.Should().BeEquivalentTo(new short[] { 1, 3 });
	}

	[Fact]
	public async Task GetAvailableATSRecordsAsync_ShouldNotBlockAnotherCandidate_WhenTheSegmentNumberMatches()
	{
		ExpectAtsSegments();

		// A different order's first employer. Same segment number, different subject -
		// the pair must be compared, not the segment alone.
		_repository
			.Setup(repository => repository.ListBlockedSegmentsAsync(
				It.IsAny<DateTime>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<BlockedEmploymentSegment>
			{
				new(Guid.NewGuid(), 1)
			});

		var available = await CreateSut().GetAvailableATSRecordsAsync(CancellationToken.None);

		available.Should().HaveCount(3);
	}
}
