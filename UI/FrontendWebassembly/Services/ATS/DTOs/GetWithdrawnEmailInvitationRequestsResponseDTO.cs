namespace FrontendWebassembly.Services.ATS.DTOs;

public record GetWithdrawnEmailInvitationRequestsResponseDTO
{
    public PaginatedResult<FrontendWebassembly.DTO.ATS.EmailInvitationRequestListDTO>? Requests { get; set; }
}
