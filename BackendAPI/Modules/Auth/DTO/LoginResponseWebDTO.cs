namespace Auth.DTO;

public record LoginResponseWebDTO
	(string UserId,
	 string AccessToken,
	 string RefreshToken,
	 string Name,
	 string TokenType = "bearer",
	 int ExpiresIn = 0,
	 List<int> Appid = default!,
	 List<List<int>> SubMenuid = default!,
	 List<int> RoleId = default!,
	 string Issued = default!,
	 string Expires = default!);
