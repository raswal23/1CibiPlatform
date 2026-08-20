namespace ATS.DTO;

// One logical user in the grouped users projection: the keyset page walks these
// keys, and the service mints the next cursor from the last one.
public class UserPageKeyDTO
{
	public Guid UserId { get; set; }
	public string UserName { get; set; } = string.Empty;
	public string UserEmail { get; set; } = string.Empty;
}
