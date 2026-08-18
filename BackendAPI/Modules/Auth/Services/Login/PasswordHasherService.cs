namespace Auth.Service;

public class PasswordHasherService : IPasswordHasherService
{

    private const int MemorySize = 1024 * 64;
    private const int Iterations = 4;
    private const int DegreeOfParallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
        {
            argon2.Salt = salt;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;
            argon2.DegreeOfParallelism = DegreeOfParallelism;

            var hash = argon2.GetBytes(HashSize);
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var parts = hashedPassword.Split('.');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var storedHash = Convert.FromBase64String(parts[1]);

        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(providedPassword)))
        {
            argon2.Salt = salt;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;
            argon2.DegreeOfParallelism = DegreeOfParallelism;

            var computedHash = argon2.GetBytes(HashSize);

            // Use constant-time comparison
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
    }
}
