// Not Test.BackendAPI.BuildingBlocks.*: a "BuildingBlocks" segment under Test.BackendAPI
// would shadow the root BuildingBlocks namespace for every test in the project.
namespace Test.BackendAPI.BuildingBlocksUnitTests;

using System.Security.Cryptography;
using BuildingBlocks.SharedServices.Implementations;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

public class AesGcmSecretProtectorTests
{
	private const string Context = "ats.AtsEmailAccount.SmtpPassword:1";

	private static AesGcmSecretProtector Build(string? key = null)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[AesGcmSecretProtector.KeyConfigurationPath] =
					key ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
			})
			.Build();

		return new AesGcmSecretProtector(configuration);
	}

	[Fact]
	public void Unprotect_ShouldReturnTheOriginal_WhenTheKeyAndContextMatch()
	{
		var protector = Build();

		var protectedValue = protector.Protect("fezy qlaz hnhm sbvy", Context);

		protector.Unprotect(protectedValue, Context).Should().Be("fezy qlaz hnhm sbvy");
	}

	[Fact]
	public void Protect_ShouldProduceADifferentCiphertext_ForTheSamePlaintext()
	{
		var protector = Build();

		var first = protector.Protect("same secret", Context);
		var second = protector.Protect("same secret", Context);

		// A fresh nonce per call. Identical ciphertexts would tell an observer with read
		// access to the table which accounts share a password.
		first.Should().NotBe(second);

		protector.Unprotect(first, Context).Should().Be("same secret");
		protector.Unprotect(second, Context).Should().Be("same secret");
	}

	[Fact]
	public void Protect_ShouldNeverLeakThePlaintext_IntoTheStoredValue()
	{
		var protector = Build();

		protector.Protect("fezy qlaz hnhm sbvy", Context)
			.Should().NotContain("fezy");
	}

	[Fact]
	public void Unprotect_ShouldThrow_WhenTheContextDiffers()
	{
		var protector = Build();

		var protectedValue = protector.Protect("secret", "ats.AtsEmailAccount.SmtpPassword:1");

		// The whole point of binding the row id: a ciphertext copied onto another row is
		// unreadable rather than silently revealing the first row's password.
		var act = () => protector.Unprotect(protectedValue, "ats.AtsEmailAccount.SmtpPassword:2");

		act.Should().Throw<CryptographicException>();
	}

	[Fact]
	public void Unprotect_ShouldThrow_WhenTheCiphertextWasTamperedWith()
	{
		var protector = Build();

		var parts = protector.Protect("secret", Context).Split('.');

		// Flip one character of the ciphertext segment, leaving the format intact.
		var tamperedSegment = parts[2][0] == 'A'
			? 'B' + parts[2][1..]
			: 'A' + parts[2][1..];

		var tampered = string.Join('.', parts[0], parts[1], tamperedSegment, parts[3]);

		var act = () => protector.Unprotect(tampered, Context);

		act.Should().Throw<CryptographicException>();
	}

	[Fact]
	public void Unprotect_ShouldThrow_WhenTheKeyDiffers()
	{
		var protectedValue = Build().Protect("secret", Context);

		var act = () => Build().Unprotect(protectedValue, Context);

		act.Should().Throw<CryptographicException>();
	}

	[Theory]
	[InlineData("not-the-expected-format")]
	[InlineData("v2.AAAA.BBBB.CCCC")]
	public void Unprotect_ShouldThrow_WhenTheValueIsNotAVersionOnePayload(string value)
	{
		var act = () => Build().Unprotect(value, Context);

		act.Should().Throw<CryptographicException>();
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenTheKeyIsMissing()
	{
		var configuration = new ConfigurationBuilder().Build();

		var act = () => new AesGcmSecretProtector(configuration);

		// Fails at start-up rather than at the first send, which is the only point at which
		// a missing key is cheap to notice.
		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*SecretProtectionKey*");
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenTheKeyIsTheWrongLength()
	{
		var act = () => Build(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*32 bytes*");
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenTheKeyIsNotBase64()
	{
		var act = () => Build("this is not base64 !!");

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*base64*");
	}
}
