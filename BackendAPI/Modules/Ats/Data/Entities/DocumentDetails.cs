namespace ATS.Data.Entities;

public class DocumentDetails
{
    public Guid DocumentDetailsID { get; set; }
    public Guid EmailInvitationID { get; set; }
    public string? DocumentName { get; set; }
    public string? DocumentValue { get; set; }
    public DateTime? CreatedDate { get; set; }
}
