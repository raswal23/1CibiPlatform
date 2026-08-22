
namespace ATS.Hubs;

public interface IATSClient
{
	Task ReceiveATSResponse(string message);
	Task ReceiveChatResponse(string message);
	Task ReceiveChatTyping(bool isTyping);
	Task SessionCleared();
}
/// <summary>
/// Bulk-upload and AI assistant notifications, delivered per user.
/// </summary>
/// <remarks>
/// The group name is taken from the authenticated principal, never from the query
/// string. The JWT rides in on the auth cookie, which the browser also sends on the
/// WebSocket handshake, so <c>Context.User</c> is populated the same way it is on an
/// ordinary request. Reading a caller-supplied <c>?userId=</c> here let anyone join
/// anyone else's group and receive their candidate data.
/// </remarks>
[Authorize]
public class ATSHub : Hub<IATSClient>
{
	public override async Task OnConnectedAsync()
	{
		var userId = Context.GetUserGroupName();

		if (!string.IsNullOrWhiteSpace(userId))
		{
			await Groups.AddToGroupAsync(Context.ConnectionId, userId);
		}

		await base.OnConnectedAsync();
	}

	// SignalR removes a connection from its groups when it disconnects, so there is
	// nothing to undo on the way out.
}
