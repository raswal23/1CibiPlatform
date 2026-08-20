namespace Auth.DTO;

public record LoginDTO(
	Guid Id,
	string PasswordHash,
	string Email,
	string FirstName,
	string LastName,
	string? MiddleName,
	bool IsApproved,
	List<int> AppId,
	List<List<int>> SubMenuId,
	List<int> roleId,
	int? AtsClientId = null,
	int? AtsRoleId = null
	);

public record UserDataDTO(
	Guid Id,
	string PasswordHash,
	string Email,
	string FirstName,
	string LastName,
	string? MiddleName,
	string refreshToken,
	List<int> AppId,
	List<List<int>> SubMenuId,
	List<int> roleId
	);

public record LoginWebCred(
	string Username,
	string Password,
	bool IsRememberMe);
