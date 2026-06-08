using Auth.DTO;
using Auth.Data.Entities;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.Auth.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class RegisterServiceTests : IClassFixture<AuthServiceFixture>
{
	private readonly AuthServiceFixture _fixture;

	public RegisterServiceTests(AuthServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task RegisterAsync_ShouldThrow_WhenEmailAlreadyExists()
	{
		// Arrange
		var request = new RegisterRequestDTO("test@example.com", "password", "John", "Doe", null);

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(request.Email, true))
			.ReturnsAsync(new OtpVerification());

		// Act
		Func<Task> act = async () => await _fixture.RegisterService.RegisterAsync(request);

		// Assert
		await act.Should().ThrowAsync<System.Exception>().WithMessage("Email already in use.");
	}

	[Fact]
	public async Task RegisterAsync_ShouldReturnOtpResponse_WhenSuccessful()
	{
		// Arrange
		var request = new RegisterRequestDTO("new@example.com", "password", "Jane", "Smith", null);

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(request.Email, true))
			.ReturnsAsync((OtpVerification?)null);

		_fixture.MockOtpService.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("123456");
		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashedOtp");
		_fixture.MockEmailService.Setup(x => x.SendOtpBody(It.IsAny<string>(), It.IsAny<string>())).Returns("body");
		_fixture.MockEmailService
			.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
			.ReturnsAsync(true);
		_fixture.MockPasswordHasherService.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashedPassword");
		_fixture.MockAuthRepository.Setup(x => x.InsertOtpVerification(It.IsAny<OtpVerification>())).ReturnsAsync(true);

		// Act
		var result = await _fixture.RegisterService.RegisterAsync(request);

		// Assert
		result.Should().NotBeNull();
		result.Email.Should().Be(request.Email);
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldThrow_WhenNoRecord()
	{
		// Arrange
		var email = "noone@example.com";
		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(email, false))
			.ReturnsAsync((OtpVerification?)null);

		// Act
		Func<Task> act = async () => await _fixture.RegisterService.VerifyOtpAsync(email, "123456");

		// Assert
		await act.Should().ThrowAsync<System.Exception>().WithMessage("No OTP record found for this email.");
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldThrow_WhenInvalidOtp()
	{
		// Arrange
		var email = "user@example.com";
		var existing = new OtpVerification
		{
			Email = email,
			OtpCodeHash = "existingHash",
			AttemptCount = 0,
			ExpiresAt = DateTime.UtcNow.AddMinutes(5)
		};

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(email, false))
			.ReturnsAsync(existing);

		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashOtp");
		_fixture.MockHashService.Setup(x => x.Verify("hashOtp", existing.OtpCodeHash)).Returns(false);
		_fixture.MockAuthRepository.Setup(x => x.UpdateVerificationCodeAsync(It.IsAny<OtpVerification>())).ReturnsAsync(true);

		// Act
		Func<Task> act = async () => await _fixture.RegisterService.VerifyOtpAsync(email, "000000");

		// Assert
		await act.Should().ThrowAsync<Exception>().WithMessage("Invalid OTP.");
		existing.AttemptCount.Should().Be(1);
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldThrow_WhenOtpAlreadyUsed()
	{
		// Arrange
		var email = "used@example.com";
		var existing = new OtpVerification
		{
			Email = email,
			OtpCodeHash = "existingHash",
			IsUsed = true,
			ExpiresAt = DateTime.UtcNow.AddMinutes(5)
		};

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(email, false))
			.ReturnsAsync(existing);
		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashOtp");
		_fixture.MockHashService.Setup(x => x.Verify("hashOtp", existing.OtpCodeHash)).Returns(true);

		// Act
		Func<Task> act = async () => await _fixture.RegisterService.VerifyOtpAsync(email, "123456");

		// Assert
		await act.Should().ThrowAsync<System.Exception>().WithMessage("OTP already used.");
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldThrow_WhenOtpExpired_AndResendCalled()
	{
		// Arrange
		var email = "expire@example.com";
		var existing = new OtpVerification
		{
			Email = email,
			OtpCodeHash = "existingHash",
			IsUsed = false,
			ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
		};

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(email, false))
			.ReturnsAsync(existing);

		_fixture.MockOtpService.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("123456");
		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashOtp");
		_fixture.MockHashService.Setup(x => x.Verify("hashOtp", existing.OtpCodeHash)).Returns(true);
		_fixture.MockAuthRepository.Setup(x => x.UpdateVerificationCodeAsync(It.IsAny<OtpVerification>())).ReturnsAsync(true);
		_fixture.MockEmailService.Setup(x => x.SendOtpBody(It.IsAny<string>(), It.IsAny<string>())).Returns("body");
		_fixture.MockEmailService.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);

		// Act
		Func<Task> act = async () => await _fixture.RegisterService.VerifyOtpAsync(email, "123456");

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Your OTP has expired. A new code has been sent to your email.");
	}

	[Fact]
	public async Task VerifyOtpAsync_ShouldReturnTrue_WhenSuccessful()
	{
		// Arrange
		var email = "success@example.com";
		var existing = new OtpVerification
		{
			Email = email,
			OtpCodeHash = "existingHash",
			AttemptCount = 0,
			ExpiresAt = DateTime.UtcNow.AddMinutes(5)
		};

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(email, false))
			.ReturnsAsync(existing);

		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashOtp");
		_fixture.MockHashService.Setup(x => x.Verify("hashOtp", existing.OtpCodeHash)).Returns(true);
		_fixture.MockAuthRepository.Setup(x => x.UpdateVerificationCodeAsync(It.IsAny<OtpVerification>())).ReturnsAsync(true);
		_fixture.MockAuthRepository.Setup(x => x.SaveUserAsync(It.IsAny<Authusers>())).ReturnsAsync(true);

		// Act
		var result = await _fixture.RegisterService.VerifyOtpAsync(email, "123456");

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task ResendOtpAsync_ShouldReturnTrue_WhenSuccessful()
	{
		// Arrange
		var otpVerification = new OtpVerification { Email = "r@example.com", FirstName = "F", LastName = "L" };
		_fixture.MockOtpService.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("654321");
		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashedOtp");
		_fixture.MockAuthRepository.Setup(x => x.UpdateVerificationCodeAsync(It.IsAny<OtpVerification>())).ReturnsAsync(true);
		_fixture.MockEmailService.Setup(x => x.SendOtpBody(It.IsAny<string>(), It.IsAny<string>())).Returns("body");
		_fixture.MockEmailService.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);

		// Act
		var result = await _fixture.RegisterService.ResendOtpAsync(otpVerification);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task ResendOtpAsync_ShouldThrow_WhenUpdateFails()
	{
		// Arrange
		var otpVerification = new OtpVerification { Email = "r2@example.com", FirstName = "F", LastName = "L" };
		_fixture.MockOtpService.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("654321");
		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashedOtp");
		_fixture.MockAuthRepository.Setup(x => x.UpdateVerificationCodeAsync(It.IsAny<OtpVerification>())).ReturnsAsync(false);

		// Act
		Func<Task> act = async () => await _fixture.RegisterService.ResendOtpAsync(otpVerification);

		// Assert
		await act.Should().ThrowAsync<System.Exception>().WithMessage("Failed to update OTP record.");
	}

	[Fact]
	public async Task ManualResendOtpCodeAsync_ShouldReturnTrue_WhenSuccessful()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var email = "manresend@example.com";
		var otpRecord = new OtpVerification { Email = email, OtpId = Guid.CreateVersion7(), FirstName = "F", LastName = "L" };

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(email, false))
			.ReturnsAsync(otpRecord);

		_fixture.MockAuthRepository.Setup(x => x.OtpVerificationUserData(It.IsAny<OtpVerificationRequestDTO>()))
			.ReturnsAsync(otpRecord);

		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(otpRecord.Email, false))
			.ReturnsAsync(otpRecord);

		_fixture.MockOtpService.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("999999");
		_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashedOtp");
		_fixture.MockAuthRepository.Setup(x => x.UpdateVerificationCodeAsync(It.IsAny<OtpVerification>())).ReturnsAsync(true);
		_fixture.MockEmailService.Setup(x => x.SendOtpBody(It.IsAny<string>(), It.IsAny<string>())).Returns("body");
		_fixture.MockEmailService.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);

		// Act
		var result = await _fixture.RegisterService.ManualResendOtpCodeAsync(userId, email);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task ManualResendOtpCodeAsync_ShouldThrow_WhenNoRecord()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var email = "missing@example.com";
		_fixture.MockAuthRepository.Setup(x => x.IsUserEmailExistInOtpVerificationAsync(email, false))
			.ReturnsAsync((OtpVerification?)null);

		// Act
		Func<Task> act = async () => await _fixture.RegisterService.ManualResendOtpCodeAsync(userId, email);

		// Assert
		await act.Should().ThrowAsync<System.Exception>().WithMessage("No OTP record found for this email.");
	}

	[Fact]
	public async Task IsOtpSessionValidAsync_ShouldReturnFalse_WhenNoRecord()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var email = "novalid@example.com";
		_fixture.MockAuthRepository.Setup(x => x.OtpVerificationUserData(It.IsAny<OtpVerificationRequestDTO>()))
			.ReturnsAsync((OtpVerification?)null);

		// Act
		var result = await _fixture.RegisterService.IsOtpSessionValidAsync(userId, email);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task IsOtpSessionValidAsync_ShouldReturnFalse_WhenUsed()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var email = "usedsession@example.com";
		var record = new OtpVerification { Email = email, IsUsed = true };
		_fixture.MockAuthRepository.Setup(x => x.OtpVerificationUserData(It.IsAny<OtpVerificationRequestDTO>()))
			.ReturnsAsync(record);

		// Act
		var result = await _fixture.RegisterService.IsOtpSessionValidAsync(userId, email);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task IsOtpSessionValidAsync_ShouldReturnTrue_WhenValid()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		var email = "validsession@example.com";
		var record = new OtpVerification { Email = email, IsUsed = false };
		_fixture.MockAuthRepository.Setup(x => x.OtpVerificationUserData(It.IsAny<OtpVerificationRequestDTO>()))
			.ReturnsAsync(record);

		// Act
		var result = await _fixture.RegisterService.IsOtpSessionValidAsync(userId, email);

		// Assert
		result.Should().BeTrue();
	}
}
