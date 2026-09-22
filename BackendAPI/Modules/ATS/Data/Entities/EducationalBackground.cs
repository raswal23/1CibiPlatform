namespace ATS.Data.Entities;

public class EducationalBackground
{
	public Guid EducationalBackgroundID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? HighestEducationalAttainment { get; set; }
	public string? HighSchoolName { get; set; }
	public DateOnly? HighSchoolGraduationDate { get; set; }
	public string? HighSchoolDiplomaFileKey { get; set; }
	public string? HighSchoolDiplomaFileName { get; set; }
	public string? SeniorHighSchoolName { get; set; }
	public DateOnly? SeniorHighSchoolGraduationDate { get; set; }
	public string? SeniorHighSchoolDiplomaFileKey { get; set; }
	public string? SeniorHighSchoolDiplomaFileName { get; set; }
	public string? CollegeSchoolName { get; set; }
	public DateOnly? CollegeGraduationDate { get; set; }
	public string? CollegeDiplomaFileKey { get; set; }
	public string? CollegeDiplomaFileName { get; set; }
	public string? CollegeDegree { get; set; }
	public string? BachelorsSchoolName { get; set; }
	public DateOnly? BachelorsGraduationDate { get; set; }
	public string? BachelorsDiplomaFileKey { get; set; }
	public string? BachelorsDiplomaFileName { get; set; }
	public string? BachelorsDegree { get; set; }
	public string? MastersSchoolName { get; set; }
	public DateOnly? MastersGraduationDate { get; set; }
	public string? MastersDiplomaFileKey { get; set; }
	public string? MastersDiplomaFileName { get; set; }
	public string? MastersDegree { get; set; }
	public string? PhDSchoolName { get; set; }
	public DateOnly? DoctorateGraduationDate { get; set; }
	public string? DoctorateDiplomaFileKey { get; set; }
	public string? DoctorateDiplomaFileName { get; set; }
	public string? DoctorateDegree { get; set; }
	public DateTime? CreatedDate { get; set; }
}