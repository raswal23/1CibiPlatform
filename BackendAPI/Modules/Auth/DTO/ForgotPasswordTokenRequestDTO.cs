namespace Auth.DTO;

public record ForgotPasswordTokenRequestDTO(Guid userId, string tokenHash);

