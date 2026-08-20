namespace ATS.AI;

/// <summary>
/// Keeps a short rolling conversation per user. The assistant service is scoped, so the
/// history itself lives here as a singleton.
/// </summary>
public sealed class AtsChatHistoryStore
{
	private const int MaxMessages = 20;

	private readonly ConcurrentDictionary<Guid, List<AtsChatTurn>> _histories = new();

	private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

	public SemaphoreSlim GetUserLock(Guid userId) =>
		_locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

	public IReadOnlyList<AtsChatTurn> Get(Guid userId) =>
		_histories.TryGetValue(userId, out var history)
			? history.ToArray()
			: Array.Empty<AtsChatTurn>();

	public void Append(Guid userId, string role, string content)
	{
		var history = _histories.GetOrAdd(userId, _ => new List<AtsChatTurn>());

		lock (history)
		{
			history.Add(new AtsChatTurn(role, content));

			while (history.Count > MaxMessages)
			{
				history.RemoveAt(0);
			}
		}
	}

	public void Clear(Guid userId)
	{
		_histories.TryRemove(userId, out _);

		if (_locks.TryRemove(userId, out var userLock))
		{
			userLock.Dispose();
		}
	}
}

public sealed record AtsChatTurn(
	string Role,
	string Content);
