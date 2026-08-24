namespace ATS.AI;

/// <summary>
/// Keeps a short rolling conversation per user. The assistant service is scoped, so the
/// history itself lives here as a singleton.
/// </summary>
/// <remarks>
/// Per-user state in a process-lifetime singleton needs an eviction rule, or it grows
/// with the number of distinct users who have ever chatted rather than the number
/// currently chatting. Entries idle for <see cref="SessionLifetime"/> are dropped, and
/// their lock with them.
/// </remarks>
public sealed class AtsChatHistoryStore
{
	private const int MaxMessages = 20;

	/// <summary>
	/// How long a conversation survives with no activity. Long enough that a user who
	/// steps away keeps their thread, short enough to bound the dictionary.
	/// </summary>
	private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(2);

	private readonly ConcurrentDictionary<Guid, UserHistory> _histories = new();

	public SemaphoreSlim GetUserLock(Guid userId) =>
		Touch(userId).Lock;

	public IReadOnlyList<AtsChatTurn> Get(Guid userId)
	{
		if (!_histories.TryGetValue(userId, out var history))
			return Array.Empty<AtsChatTurn>();

		history.LastAccessedUtc = DateTime.UtcNow;

		lock (history.Turns)
		{
			return history.Turns.ToArray();
		}
	}

	public void Append(Guid userId, string role, string content)
	{
		var history = Touch(userId);

		lock (history.Turns)
		{
			history.Turns.Add(new AtsChatTurn(role, content));

			while (history.Turns.Count > MaxMessages)
			{
				history.Turns.RemoveAt(0);
			}
		}
	}

	public void Clear(Guid userId)
	{
		if (_histories.TryRemove(userId, out var history))
		{
			history.Lock.Dispose();
		}
	}

	/// <summary>
	/// Fetches (or creates) a user's history, stamps it as live, and opportunistically
	/// evicts anything idle. Sweeping here rather than on a timer keeps the store free
	/// of background machinery; the cost is one pass over a dictionary bounded by the
	/// number of users active in the last couple of hours.
	/// </summary>
	private UserHistory Touch(Guid userId)
	{
		RemoveExpired();

		var history = _histories.GetOrAdd(userId, _ => new UserHistory());
		history.LastAccessedUtc = DateTime.UtcNow;
		return history;
	}

	private void RemoveExpired()
	{
		var cutoff = DateTime.UtcNow.Subtract(SessionLifetime);

		foreach (var entry in _histories)
		{
			if (entry.Value.LastAccessedUtc >= cutoff)
				continue;

			// A user who returns mid-sweep just gets a fresh history - losing an idle
			// conversation is preferable to holding every lock ever created.
			if (_histories.TryRemove(entry.Key, out var removed))
			{
				removed.Lock.Dispose();
			}
		}
	}

	private sealed class UserHistory
	{
		public List<AtsChatTurn> Turns { get; } = [];

		public SemaphoreSlim Lock { get; } = new(1, 1);

		public DateTime LastAccessedUtc { get; set; } = DateTime.UtcNow;
	}
}

public sealed record AtsChatTurn(
	string Role,
	string Content);
