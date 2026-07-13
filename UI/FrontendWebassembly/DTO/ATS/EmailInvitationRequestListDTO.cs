namespace FrontendWebassembly.DTO.ATS;

public record EmailInvitationRequestListDTO
{
    public Guid EmailInvitationID { get; set; }
    public string? EmailAddress { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MobileNumber { get; set; }
    public string? OrderStatus { get; set; }
    public string? ApplicationFormStatus { get; set; }
    public string? EmailSentStatus { get; set; }
}
