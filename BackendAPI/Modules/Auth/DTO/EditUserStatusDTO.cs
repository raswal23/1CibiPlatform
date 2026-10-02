namespace Auth.DTO;

/// <summary>
/// Changes only whether a user account is active. Deliberately narrower than
/// <see cref="EditUserDTO"/>: approval is the approval queue's decision, and name/email
/// belong to the user, so neither can be altered through this path.
/// </summary>
public class EditUserStatusDTO
{
	public Guid UserId { get; set; }

	public bool IsActive { get; set; }
}
