namespace ATS.DTO;

public record EducationalBackgroundDTO
{
	public Guid EducationalBackgroundID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? HighestEducationalAttainment { get; set; }
	public string? HighSchoolName { get; set; }
	public DateOnly? HighSchoolGraduationDate { get; set; }
	public IFormFile? HighSchoolDiplomaFile { get; set; }
	public string? HighSchoolDiplomaFileName { get; set; }
	public string? SeniorHighSchoolName { get; set; }
	public DateOnly? SeniorHighSchoolGraduationDate { get; set; }
	public IFormFile? SeniorHighSchoolDiplomaFile { get; set; }
	public string? SeniorHighSchoolDiplomaFileName { get; set; }
	public string? BachelorsSchoolName { get; set; }
	public DateOnly? BachelorsGraduationDate { get; set; }
	public IFormFile? BachelorsDiplomaFile { get; set; }
	public string? BachelorsDiplomaFileName { get; set; }
	public string? BachelorsDegree { get; set; }
	public string? MastersSchoolName { get; set; }
	public DateOnly? MastersGraduationDate { get; set; }
	public IFormFile? MastersDiplomaFile { get; set; }
	public string? MastersDiplomaFileName { get; set; }
	public string? MastersDegree { get; set; }
	public string? PhDSchoolName { get; set; }
	public DateOnly? DoctorateGraduationDate { get; set; }
	public IFormFile? DoctorateDiplomaFile { get; set; }
	public string? DoctorateDiplomaFileName { get; set; }
	public string? DoctorateDegree { get; set; }
	public DateTime? CreatedDate { get; set; }
}
