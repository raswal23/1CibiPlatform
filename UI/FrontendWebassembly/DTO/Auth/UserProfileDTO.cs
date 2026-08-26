namespace FrontendWebassembly.DTO.Auth;

public record UserProfileDTO
{
	public Guid UserId { get; set; }
	public string? Email { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? FullName { get; set; }
}

public record UserProfileResponseDTO
{
	public UserProfileDTO? Profile { get; set; }
}
