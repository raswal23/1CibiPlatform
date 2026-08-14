namespace Auth.Data.Repository;

public interface IPasswordRecoveryRepository : IRegistrationRepository, IUserRepository
{
	Task<PasswordResetToken> GetUserTokenAsync(string tokenHash);
	Task<bool> SaveToResetPasswordToken(PasswordResetToken passwordResetToken);
	Task<bool> UpdateAuthUserPassword(Authusers authusers);
	Task<bool> UpdatePasswordResetTokenAsUsedAsync(PasswordResetToken passwordResetToken);
	Task<List<int>> RevokeAllSessionsAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}
