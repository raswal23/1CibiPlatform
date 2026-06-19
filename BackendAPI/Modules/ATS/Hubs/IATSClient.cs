
namespace ATS.Hubs;

public interface IATSClient
{
	Task ReceiveATSResponse(string message);
	Task ReceiveTyping(bool isTyping);
	Task SessionCleared();
}
public class ATSHub : Hub<IATSClient>
{
	public override Task OnConnectedAsync()
	{
		var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
		if (!string.IsNullOrEmpty(userId))
		{
			Groups.AddToGroupAsync(Context.ConnectionId, userId);
		}

		return base.OnConnectedAsync();
	}

	public override Task OnDisconnectedAsync(Exception? exception)
	{
		var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
		if (!string.IsNullOrEmpty(userId))
		{
			Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
		}

		return base.OnDisconnectedAsync(exception);
	}
}
