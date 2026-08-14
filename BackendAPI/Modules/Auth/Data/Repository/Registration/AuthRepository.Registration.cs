namespace Auth.Data.Repository;

public partial class AuthRepository
{
	public async Task<Authusers> IsUserEmailExistAsync(string email)
		{
			return await
				_dbcontext.AuthUsers
				.FirstOrDefaultAsync(au => au.Email == email && au.IsActive);
		}
	
	public async Task<RegisterResponseDTO> RegisterUserAsync(RegisterRequestDTO userDto)
		{
			var user = new Authusers
			{
				Id = Guid.CreateVersion7(),
				Email = userDto.Email,
				PasswordHash = userDto.PasswordHash,
				FirstName = userDto.FirstName,
				LastName = userDto.LastName,
				MiddleName = userDto.MiddleName,
			};
	
			await _dbcontext.AddAsync(user);
	
			await _dbcontext.SaveChangesAsync();
	
			return new RegisterResponseDTO(
				user.Id,
				user.Email!,
				user.PasswordHash!,
				user.FirstName!,
				user.LastName!,
				user.MiddleName);
		}
	
	public async Task<bool> UpdateVerificationCodeAsync(OtpVerification otpVerification)
		{
	
			_dbcontext.OtpVerification.Update(otpVerification);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
	
		}
	
	public async Task<bool> InsertOtpVerification(OtpVerification otpVerification)
		{
	
			var otpUser = await _dbcontext.OtpVerification.AddAsync(otpVerification);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
	
	public async Task<OtpVerification> IsUserEmailExistInOtpVerificationAsync(string email, bool isUsed)
		{
			return await _dbcontext.OtpVerification
						 .Where(ov => ov.Email == email && ov.IsUsed == isUsed)
						 .OrderByDescending(ov => ov.CreatedAt)
						 .FirstOrDefaultAsync();
	
		}
	
	public async Task<bool> UpdateValidateOtp(OtpVerification otpVerification)
		{
			_dbcontext.OtpVerification.Update(otpVerification);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
		}
	
	public async Task<bool> DeleteOtpRecordIfExpired(OtpVerification otpVerification)
		{
			_dbcontext.OtpVerification.Remove(otpVerification);
	
			await _dbcontext.SaveChangesAsync();
	
			return true;
	
		}
	
	public async Task<OtpVerification> OtpVerificationUserData(OtpVerificationRequestDTO otpVerification)
		{
			return await _dbcontext.OtpVerification
						 .Where(ov => ov.Email == otpVerification.email &&
								ov.OtpId == otpVerification.userId &&
								ov.IsUsed == false &&
								ov.IsVerified == false &&
								ov.ExpiresAt > DateTime.UtcNow)
						 .AsNoTracking()
						 .FirstOrDefaultAsync();
		}
}
