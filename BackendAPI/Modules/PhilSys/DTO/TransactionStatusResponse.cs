namespace PhilSys.DTO;

public record TransactionStatusResponse
{
	public bool Exists { get; set; } = true;
	public string? WebHookURl { get; set; }
	public bool IsTransacted { get; set; }
	public string? ATSSession { get; set; }
	public DateTime ExpiresAt { get; set; }
}
