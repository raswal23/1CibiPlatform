namespace FrontendWebassembly.DTO.Auth;

public record EditUserDTO
{
	public string? Email { get; set; }
	public bool IsApproved { get; set; }
}
