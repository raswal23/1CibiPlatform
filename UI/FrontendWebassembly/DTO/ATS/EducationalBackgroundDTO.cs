namespace FrontendWebassembly.DTO.ATS;

public record EducationalBackgroundDTO
{
	public Guid EducationalBackgroundID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? HighestEducationalAttainment { get; set; }
	public string? HighSchoolName { get; set; }
	public DateTime? HighSchoolGraduationDate { get; set; }
	public byte[]? HighSchoolDiplomaFile { get; set; }
	public string? HighSchoolDiplomaFileName { get; set; }
	public string? SeniorHighSchoolName { get; set; }
	public DateTime? SeniorHighSchoolGraduationDate { get; set; }
	public byte[]? SeniorHighSchoolDiplomaFile { get; set; }
	public string? SeniorHighSchoolDiplomaFileName { get; set; }
	public string? BachelorsSchoolName { get; set; }
	public DateTime? BachelorsGraduationDate { get; set; }
	public byte[]? BachelorsDiplomaFile { get; set; }
	public string? BachelorsDiplomaFileName { get; set; }
	public string? BachelorsMajor { get; set; }
	public string? MastersSchoolName { get; set; }
	public DateTime? MastersGraduationDate { get; set; }
	public byte[]? MastersDiplomaFile { get; set; }
	public string? MastersDiplomaFileName { get; set; }
	public string? MastersMajor { get; set; }
	public string? PhDSchoolName { get; set; }
	public DateTime? DoctorateGraduationDate { get; set; }
	public byte[]? DoctorateDiplomaFile { get; set; }
	public string? DoctorateDiplomaFileName { get; set; }
	public string? DoctorateMajor { get; set; }
	public DateTime? CreatedDate { get; set; }
}
