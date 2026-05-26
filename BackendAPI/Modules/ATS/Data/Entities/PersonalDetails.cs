namespace ATS.Data.Entities;

public class PersonalDetails
{
	public Guid PersonalID { get; set; }
	public Guid EmailInvitationID { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string? Suffix { get; set; }
	public string? MaritalStatus { get; set; }
	public string? Nationality { get; set; }
	public string? Sex { get; set; }
	public DateTime? DOB { get; set; }
	public string? SSS { get; set; }
	public string? TIN { get; set; }
	public string? MobileNumber { get; set; }
	public string? TelephoneNumber { get; set; }
	public string? EmailAddress { get; set; }
	public string? EmailAlternative { get; set; }
	public string? AdditionalGovtID { get; set; }
	public string? NBIClearance { get; set; }
	public string? Resume { get; set; }
	public DateTime? CreatedDate { get; set; }
}