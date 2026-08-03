namespace FrontendWebassembly.DTO.PhilSys;

public record TransactionStatusResponseDTO
{
	public string? WebHookUrl { get; set; }
	public string? ATSApplicationFormPath { get; set; }
	public bool IsTransacted { get; set; }
	public bool isExpired { get; set; }
	public string? ATSSession { get; set; }
	public DateTime ExpiresAt { get; set; }
}
