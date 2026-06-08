namespace ATS.DTO;
public record ErrorResponseDTO(
string? error,
string? message,
string? error_description
);
