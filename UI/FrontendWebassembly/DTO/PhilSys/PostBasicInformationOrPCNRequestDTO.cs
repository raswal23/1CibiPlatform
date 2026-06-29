namespace FrontendWebassembly.DTO.PhilSys;
public record IdentityData
{
	public string? first_name { get; set; }
	public string? middle_name { get; set; }
	public string? last_name { get; set; }
	public string? birth_date { get; set; }
	public string? suffix { get; set; }
	public string? pcn { get; set; }
	public string? ats_session { get; set; }
}