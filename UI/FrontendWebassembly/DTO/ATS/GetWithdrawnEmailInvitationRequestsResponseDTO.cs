namespace FrontendWebassembly.DTO.ATS;

public record GetWithdrawnEmailInvitationRequestsResponseDTO
{
	public PaginatedResult<EmailInvitationRequestListDTO>? Requests { get; set; }
}
