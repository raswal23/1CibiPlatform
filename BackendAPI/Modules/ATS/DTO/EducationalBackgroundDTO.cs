namespace ATS.DTO;

public record EducationalBackgroundDTO
{
	public Guid EducationalBackgroundID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? HighestEducationalAttainment { get; set; }
	public string? HighSchoolName { get; set; }
	public string? HighSchoolAddress { get; set; }
	public string? HighSchoolGraduationDate { get; set; }
	public string? HighSchoolDiploma { get; set; }
	public string? SeniorHighSchoolName { get; set; }
	public string? SeniorHighSchoolAddress { get; set; }
	public string? SeniorHighSchoolGraduationDate { get; set; }
	public string? SeniorHighSchoolDiploma { get; set; }
	public string? CollegeSchoolName { get; set; }
	public string? CollegeAddress { get; set; }
	public string? CollegeGraduationDate { get; set; }
	public string? CollegeDiploma { get; set; }
	public string? CollegeDegree { get; set; }
	public string? CollegeMajor { get; set; }
	public string? BachelorsSchoolName { get; set; }
	public string? BachelorsAddress { get; set; }
	public string? BachelorsGraduationDate { get; set; }
	public string? BachelorsDiploma { get; set; }
	public string? BachelorsDegree { get; set; }
	public string? BachelorsMajor { get; set; }
	public string? MastersSchoolName { get; set; }
	public string? MastersAddress { get; set; }
	public string? MastersGraduationDate { get; set; }
	public string? MastersDiploma { get; set; }
	public string? MastersDegree { get; set; }
	public string? MastersMajor { get; set; }
	public string? PhDSchoolName { get; set; }
	public string? DoctorateAddress { get; set; }
	public string? DoctorateGraduationDate { get; set; }
	public string? DoctorateDiploma { get; set; }
	public string? DoctorateDegree { get; set; }
	public string? DoctorateMajor { get; set; }
	public byte[]? SchoolSpecificLOA { get; set; }
	public DateTime? CreatedDate { get; set; }
}
