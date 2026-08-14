namespace BuildingBlocks.SharedServices.Implementations;

public class HashService : IHashService
{
	/// <summary>
	/// Hashes the input with SHA-512 and renders it as unpadded base64url,
	/// which is 86 characters wide.
	/// </summary>
	public string Hash(string input)
	{
		var hashBytes = SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(input));

		return Convert.ToBase64String(hashBytes)
					  .Replace('+', '-')
					  .Replace('/', '_')
					  .TrimEnd('=');
	}

	public bool Verify(string inputHash, string hash)
	{
		if (string.IsNullOrEmpty(inputHash) || string.IsNullOrEmpty(hash))
		{
			return false;
		}

		// Convert Base64Url back to standard Base64 before decoding
		string ToBase64(string base64Url)
		{
			string padded = base64Url.Replace('-', '+').Replace('_', '/');
			switch (padded.Length % 4)
			{
				case 2: padded += "=="; break;
				case 3: padded += "="; break;
			}
			return padded;
		}

		try
		{
			// FixedTimeEquals reports a length mismatch as false, so a hash stored
			// under a previous algorithm simply fails to verify. Decoding is what
			// can throw, when either value is not valid base64url.
			return CryptographicOperations.FixedTimeEquals(
				Convert.FromBase64String(ToBase64(inputHash)),
				Convert.FromBase64String(ToBase64(hash))
			);
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
