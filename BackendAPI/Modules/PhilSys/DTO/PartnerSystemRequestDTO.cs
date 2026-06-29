namespace PhilSys.DTO;

public record IdentityData(
	[property: JsonPropertyName("first_name")] string? FirstName,
	[property: JsonPropertyName("middle_name")] string? MiddleName,
	[property: JsonPropertyName("last_name")] string? LastName,
	[property: JsonPropertyName("suffix")] string? Suffix,
	[property: JsonPropertyName("birth_date")] string? BirthDate,
	[property: JsonPropertyName("pcn")] string? PCN,
	[property: JsonPropertyName("ats_session")] string? ATSSession
);