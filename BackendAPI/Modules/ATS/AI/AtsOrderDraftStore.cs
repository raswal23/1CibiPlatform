namespace ATS.AI;

/// <summary>
/// Holds orders that the assistant has staged but the user has not confirmed yet.
/// Drafts are single use and expire, so a stale confirmation card cannot be replayed.
/// </summary>
public sealed class AtsOrderDraftStore
{
	private static readonly TimeSpan DraftLifetime = TimeSpan.FromMinutes(15);

	private readonly ConcurrentDictionary<Guid, StagedDraft> _drafts = new();

	public AtsOrderDraftDTO Stage(Guid ownerUserId, AtsOrderDraftDTO draft)
	{
		RemoveExpired();

		draft.DraftId = Guid.CreateVersion7();

		_drafts[draft.DraftId] = new StagedDraft(
			ownerUserId,
			draft,
			DateTime.UtcNow.Add(DraftLifetime));

		return draft;
	}

	/// <summary>
	/// Removes and returns the draft when it exists, has not expired, and belongs to
	/// the requesting user. Returns null otherwise.
	/// </summary>
	public AtsOrderDraftDTO? Consume(Guid draftId, Guid ownerUserId)
	{
		RemoveExpired();

		if (!_drafts.TryGetValue(draftId, out var staged))
		{
			return null;
		}

		if (staged.OwnerUserId != ownerUserId || staged.ExpiresAt <= DateTime.UtcNow)
		{
			return null;
		}

		return _drafts.TryRemove(draftId, out var removed)
			? removed.Draft
			: null;
	}

	private void RemoveExpired()
	{
		var now = DateTime.UtcNow;

		foreach (var entry in _drafts)
		{
			if (entry.Value.ExpiresAt <= now)
			{
				_drafts.TryRemove(entry.Key, out _);
			}
		}
	}

	private sealed record StagedDraft(
		Guid OwnerUserId,
		AtsOrderDraftDTO Draft,
		DateTime ExpiresAt);
}
