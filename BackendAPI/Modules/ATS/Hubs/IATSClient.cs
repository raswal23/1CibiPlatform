
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
/// string. Reading a caller-supplied <c>?userId=</c> here let anyone join anyone else's
/// group and receive their candidate data; deriving it from <c>Context.User</c> is what
/// closes that - a connection with no principal joins no group and receives nothing.
///
/// Deliberately NOT [Authorize]: the client builds this connection with a bare
/// HubConnectionBuilder, which does not go through CookieHandler and so does not set
/// BrowserRequestCredentials.Include. The UI and API are different origins, so the auth
/// cookie is not sent on the handshake and [Authorize] would reject every connection
/// with a 401. Adding it back requires giving the connection a credentialed handler
/// first - see AIChatService/EndorsementSubmissionService.
/// </remarks>
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
