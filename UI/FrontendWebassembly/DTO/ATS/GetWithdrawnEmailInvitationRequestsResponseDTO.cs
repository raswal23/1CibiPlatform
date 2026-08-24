namespace FrontendWebassembly.DTO.ATS;

public record GetWithdrawnEmailInvitationRequestsResponseDTO
{
	public KeysetPaginatedResult<EmailInvitationRequestListDTO>? Requests { get; set; }
}
