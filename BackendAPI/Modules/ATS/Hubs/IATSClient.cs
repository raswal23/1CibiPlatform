
namespace ATS.Hubs;

public interface IATSClient
{
	Task ReceiveATSResponse(string message);
	Task SessionCleared();
}
public class ATSHub : Hub<IATSClient>
{
	public override async Task OnConnectedAsync()
	{
		var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();

		if (!string.IsNullOrWhiteSpace(userId))
		{
			await Groups.AddToGroupAsync(Context.ConnectionId, userId);
		}

		await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();

		if (!string.IsNullOrWhiteSpace(userId))
		{
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
		}

		await base.OnDisconnectedAsync(exception);
	}
}
