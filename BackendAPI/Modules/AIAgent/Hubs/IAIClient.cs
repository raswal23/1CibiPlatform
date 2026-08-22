namespace AIAgent.Hubs;

public interface IAIClient
{
	Task ReceiveAiResponse(string message);
	Task ReceiveTyping(bool isTyping);
	Task SessionCleared();
}

/// <summary>
/// AI agent chat responses, delivered per user.
/// </summary>
/// <remarks>
/// The group name comes from the authenticated principal, never from the query string -
/// see <see cref="ATS.Hubs.ATSHub"/> for the same reasoning. The previous version also
/// dropped the AddToGroupAsync/RemoveFromGroupAsync tasks without awaiting them, so a
/// client could be sent its first message before it had finished joining.
/// </remarks>
[Authorize]
public class AIAgentHub : Hub<IAIClient>
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

	// SignalR removes a connection from its groups when it disconnects.
}