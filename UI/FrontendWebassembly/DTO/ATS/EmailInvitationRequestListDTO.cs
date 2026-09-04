namespace FrontendWebassembly.DTO.ATS;

public record EmailInvitationRequestListDTO
{
    public Guid EmailInvitationID { get; set; }
    public string? EmailAddress { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Requestor { get; set; }
    public string? TicketNumber { get; set; }
    public string? OrderStatus { get; set; }
    public DateTime? OrderCreatedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
}
