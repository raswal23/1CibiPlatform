namespace FrontendWebassembly.DTO.PhilSys;

public record TransactionStatusResponseDTO
{
	public bool Exists { get; set; }
	public string? WebHookUrl { get; set; }
	public string? ATSApplicationFormPath { get; set; }
	public string? ATSSession { get; set; }
	public bool IsTransacted { get; set; }
	public bool isExpired { get; set; } 
	public DateTime ExpiresAt { get; set; }
	public string? trace_id { get; set; }
	public string? error_message { get; set; }
}
