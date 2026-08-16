namespace ATS.DTO;

public record AtsOrderDraftDTO
{
	public Guid DraftId { get; set; }

	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	public string? MiddleInitial { get; set; }

	public string? EmailAddress { get; set; }

	public string? MobileNumber { get; set; }

	public string? SelectPackage { get; set; }

	public string? RushNormal { get; set; }
}
