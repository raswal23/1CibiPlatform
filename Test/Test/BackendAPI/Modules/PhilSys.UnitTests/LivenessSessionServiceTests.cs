using BuildingBlocks.Exceptions;
using FluentAssertions;
using Moq;
using PhilSys.Data.Entities;
using PhilSys.DTO;
using Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.PhilSys.UnitTests
{
	public class LivenessSessionServiceTests : IClassFixture<PhilSysServiceFixture>
	{
		private readonly PhilSysServiceFixture _fixture;

		public LivenessSessionServiceTests(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}

		[Fact]
		public async Task LivenessSessionService_ShouldThrow_WhenTransactionRecordNotFound()
		{
			// Arrange
			var service = _fixture.LivenessSessionService;
			_fixture.MockPhilSysRepository.Setup(x => x.GetLivenessSessionStatusAsync(It.IsAny<string>())).ReturnsAsync((TransactionStatusResponse?)null);

			// Act
			Func<Task> act = async () => await service.IsLivenessUsedAsync("hash-token");

			// Assert
			await act.Should().ThrowAsync<NotFoundException>().WithMessage("There is no such transaction created for your liveness check.");
		}

		[Fact]
		public async Task LivenessSessionService_ShouldThrow_WhenTransactionRecordNotFoundByHashToken()
		{
			var service = _fixture.LivenessSessionService;
			var transactionStatus = new TransactionStatusResponse{ Exists =  true, WebHookURl = "/", IsTransacted = false, ExpiresAt = DateTime.UtcNow};
			_fixture.MockPhilSysRepository.Setup(x => x.GetLivenessSessionStatusAsync(It.IsAny<string>())).ReturnsAsync(transactionStatus);
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(It.IsAny<string>())).ReturnsAsync((PhilSysTransaction?)null);

			// Act
			Func<Task> act = async () => await service.IsLivenessUsedAsync("hash-token");

			// Assert
			await act.Should().ThrowAsync<NotFoundException>().WithMessage("There is no such transaction created for your liveness check.");
		}

		[Fact]
		public async Task LivenessSessionService_ShouldThrow_WhenTokenIsInvalid()
		{
			var service = _fixture.LivenessSessionService;
			var Tid = Guid.CreateVersion7();
			var transactionStatus = new TransactionStatusResponse { Exists = true, WebHookURl = "/", IsTransacted = false, ExpiresAt = DateTime.UtcNow };
			var transactionRecord = new PhilSysTransaction { Tid = Tid, InquiryType = "pcn", PCN = "6786785465456459"};
			_fixture.MockPhilSysRepository.Setup(x => x.GetLivenessSessionStatusAsync(It.IsAny<string>())).ReturnsAsync(transactionStatus);
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(It.IsAny<string>())).ReturnsAsync(transactionRecord);
			_fixture.MockHashService.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

			// Act
			Func<Task> act = async () => await service.IsLivenessUsedAsync("hash-token");

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("Unable to Proceed. Invalid Token provided.");
		}

		[Fact]
		public async Task LivenessSessionService_ShouldReturnTransactionResponseDTO_WhenSuccessful()
		{
			var service = _fixture.LivenessSessionService;
			var Tid = Guid.CreateVersion7();
			var FixedUtcNow = new DateTime(2025, 11, 9, 0, 0, 0, DateTimeKind.Utc);
			var transactionStatus = new TransactionStatusResponse { Exists = true, WebHookURl = "/", IsTransacted = false, ExpiresAt = FixedUtcNow};
			var transactionStatusDTO = new TransactionStatusResponseDTO { Exists = true, WebHookURl = "/", IsTransacted = false, ExpiresAt = FixedUtcNow, ATSApplicationFormPath = string.Empty }; 
			var transactionRecord = new PhilSysTransaction { Tid = Tid, InquiryType = "pcn", PCN = "6786785465456459" };
			_fixture.MockPhilSysRepository.Setup(x => x.GetLivenessSessionStatusAsync(It.IsAny<string>())).ReturnsAsync(transactionStatus);
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(It.IsAny<string>())).ReturnsAsync(transactionRecord);
			_fixture.MockHashService.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

			// Act
			var result = await service.IsLivenessUsedAsync("hash-token");

			// Assert
			result.Should().Be(transactionStatusDTO);
		}
	}
}
