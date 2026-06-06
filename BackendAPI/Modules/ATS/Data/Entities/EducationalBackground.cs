namespace ATS.Data.Entities;

public class EducationalBackground
{
	public Guid EducationalBackgroundID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? HighestEducationalAttainment { get; set; }
	public string? HighSchoolName { get; set; }
	public string? HighSchoolAddress { get; set; }
	public DateOnly? HighSchoolGraduationDate { get; set; }
	public string? HighSchoolDiplomaFileKey { get; set; }
	public string? SeniorHighSchoolName { get; set; }
	public string? SeniorHighSchoolAddress { get; set; }
	public DateOnly? SeniorHighSchoolGraduationDate { get; set; }
	public string? SeniorHighSchoolDiplomaFileKey { get; set; }
	public string? CollegeSchoolName { get; set; }
	public string? CollegeAddress { get; set; }
	public DateOnly? CollegeGraduationDate { get; set; }
	public string? CollegeDiplomaFileKey { get; set; }
	public string? CollegeDegree { get; set; }
	public string? CollegeMajor { get; set; }
	public string? BachelorsSchoolName { get; set; }
	public string? BachelorsAddress { get; set; }
	public DateOnly? BachelorsGraduationDate { get; set; }
	public string? BachelorsDiplomaFileKey { get; set; }
	public string? BachelorsDegree { get; set; }
	public string? BachelorsMajor { get; set; }
	public string? MastersSchoolName { get; set; }
	public string? MastersAddress { get; set; }
	public DateOnly? MastersGraduationDate { get; set; }
	public string? MastersDiplomaFileKey { get; set; }
	public string? MastersDegree { get; set; }
	public string? MastersMajor { get; set; }
	public string? PhDSchoolName { get; set; }
	public string? DoctorateAddress { get; set; }
	public DateOnly? DoctorateGraduationDate { get; set; }
	public string? DoctorateDiplomaFileKey { get; set; }
	public string? DoctorateDegree { get; set; }
	public string? DoctorateMajor { get; set; }
	public string? SchoolSpecificLOAFileKey { get; set; }
	public DateTime? CreatedDate { get; set; }
}