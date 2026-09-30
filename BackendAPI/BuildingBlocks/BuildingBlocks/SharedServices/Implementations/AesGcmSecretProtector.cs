namespace BuildingBlocks.SharedServices.Implementations;

/// <summary>
/// AES-256-GCM over a key supplied by configuration.
///
/// Why not ASP.NET Data Protection, which is the usual answer: its key ring has to live
/// somewhere. In a container that means a mounted volume - the API mounts none that is
/// writable - or PersistKeysToDbContext, which writes the key ring UNENCRYPTED unless it is
/// itself protected by a certificate or DPAPI, neither of which exists in a Linux container
/// here. A key ring stored unencrypted beside the ciphertext it protects is not encryption at
/// rest. One key from the environment, which is where every other credential in this system
/// already lives, is both simpler and honest about where the trust boundary is.
///
/// GCM rather than CBC because it authenticates: a ciphertext that has been altered, or moved
/// to a different row, fails loudly instead of decrypting to garbage that is then handed to an
/// SMTP server.
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
	public const string KeyConfigurationPath = "Security:SecretProtectionKey";

	// Prefixes the wire format. A future key rotation or algorithm change becomes a migration
	// that can tell old values from new ones, rather than a guess about what a blob is.
	private const string Version = "v1";

	private const int KeySizeBytes = 32;
	private const int NonceSizeBytes = 12;
	private const int TagSizeBytes = 16;

	private readonly byte[] _key;

	public AesGcmSecretProtector(IConfiguration configuration)
	{
		var configured = configuration[KeyConfigurationPath];

		// Fail at start-up, not at the first send. A missing key here would otherwise mean
		// either storing plaintext or discovering the problem when a background job tries to
		// read a password back hours later.
		if (string.IsNullOrWhiteSpace(configured))
		{
			throw new InvalidOperationException(
				$"{KeyConfigurationPath} is not configured. It must be a base64 " +
				$"{KeySizeBytes}-byte key.");
		}

		byte[] key;

		try
		{
			key = Convert.FromBase64String(configured);
		}
		catch (FormatException exception)
		{
			throw new InvalidOperationException(
				$"{KeyConfigurationPath} is not valid base64.", exception);
		}

		if (key.Length != KeySizeBytes)
		{
			throw new InvalidOperationException(
				$"{KeyConfigurationPath} must decode to exactly {KeySizeBytes} bytes " +
				$"(AES-256); it decoded to {key.Length}.");
		}

		_key = key;
	}

	public string Protect(string plaintext, string context)
	{
		ArgumentNullException.ThrowIfNull(plaintext);
		ArgumentException.ThrowIfNullOrWhiteSpace(context);

		var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
		var associatedData = Encoding.UTF8.GetBytes(context);

		// Random per call, never reused. A repeated nonce under the same key is the one
		// mistake that breaks GCM outright, which is why this is generated here rather than
		// derived from anything about the row.
		var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);

		var ciphertext = new byte[plaintextBytes.Length];
		var tag = new byte[TagSizeBytes];

		// .NET 8 onwards requires the tag size explicitly rather than inferring it.
		using var aes = new AesGcm(_key, TagSizeBytes);

		aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, associatedData);

		return string.Join('.',
			Version,
			ToBase64Url(nonce),
			ToBase64Url(ciphertext),
			ToBase64Url(tag));
	}

	public string Unprotect(string protectedValue, string context)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
		ArgumentException.ThrowIfNullOrWhiteSpace(context);

		var parts = protectedValue.Split('.');

		if (parts.Length != 4 || parts[0] != Version)
		{
			throw new CryptographicException(
				"The protected value is not in the expected format.");
		}

		byte[] nonce;
		byte[] ciphertext;
		byte[] tag;

		try
		{
			nonce = FromBase64Url(parts[1]);
			ciphertext = FromBase64Url(parts[2]);
			tag = FromBase64Url(parts[3]);
		}
		catch (FormatException exception)
		{
			throw new CryptographicException(
				"The protected value is not valid base64url.", exception);
		}

		if (nonce.Length != NonceSizeBytes || tag.Length != TagSizeBytes)
		{
			throw new CryptographicException(
				"The protected value has a malformed nonce or authentication tag.");
		}

		var plaintextBytes = new byte[ciphertext.Length];
		var associatedData = Encoding.UTF8.GetBytes(context);

		using var aes = new AesGcm(_key, TagSizeBytes);

		// Throws AuthenticationTagMismatchException (a CryptographicException) when the key,
		// the context or the ciphertext does not match. Deliberately not caught: a password
		// that cannot be decrypted must not silently become an empty string that then fails
		// SMTP authentication and trips the account's breaker.
		aes.Decrypt(nonce, ciphertext, tag, plaintextBytes, associatedData);

		return Encoding.UTF8.GetString(plaintextBytes);
	}

	private static string ToBase64Url(byte[] value) =>
		Convert.ToBase64String(value)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');

	private static byte[] FromBase64Url(string value)
	{
		var padded = value.Replace('-', '+').Replace('_', '/');

		padded += (padded.Length % 4) switch
		{
			2 => "==",
			3 => "=",
			_ => string.Empty
		};

		return Convert.FromBase64String(padded);
	}
}
