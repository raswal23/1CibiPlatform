namespace ATS.Data.Repository.Notifications;

// Deliberately NOT cached, and no ATSCacheRepository decorator: the whole point of the
// bell is to show what just happened, so a cached unread count or first page would hide
// the notification that was raised a second ago. Same reasoning as AtsAuditRepository and
// OMSTicketingRepository.
public sealed class AtsNotificationRepository : IAtsNotificationRepository
{
	private readonly ATSDBContext _dbContext;

	public AtsNotificationRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public async Task AddAsync(AtsNotification notification, CancellationToken cancellationToken)
	{
		_dbContext.Notifications.Add(notification);

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<List<NotificationListDTO>> GetNotificationsPageAsync(
		Guid recipientUserId,
		DateTime? afterCreatedAt,
		Guid? afterNotificationId,
		int take,
		bool unreadOnly,
		CancellationToken cancellationToken)
	{
		var query = BuildRowsQuery(recipientUserId, unreadOnly);

		if (afterCreatedAt.HasValue && afterNotificationId.HasValue)
		{
			query = ApplySeek(query, afterCreatedAt.Value, afterNotificationId.Value);
		}

		return await ApplyOrder(query)
			.Take(take)
			.Select(Projection)
			.ToListAsync(cancellationToken);
	}

	public Task<long> CountNotificationsAsync(
		Guid recipientUserId,
		bool unreadOnly,
		CancellationToken cancellationToken) =>
		BuildRowsQuery(recipientUserId, unreadOnly)
			.LongCountAsync(cancellationToken);

	public Task<long> GetUnreadCountAsync(
		Guid recipientUserId,
		CancellationToken cancellationToken) =>
		_dbContext.Notifications
			.AsNoTracking()
			.Where(notification => notification.RecipientUserId == recipientUserId
				&& !notification.IsRead)
			.LongCountAsync(cancellationToken);

	// The recipient predicate is part of the UPDATE rather than a lookup-then-check, so
	// another user's id simply matches no rows instead of being fetched and rejected.
	public async Task<bool> MarkAsReadAsync(
		Guid recipientUserId,
		Guid notificationId,
		CancellationToken cancellationToken)
	{
		var updated = await _dbContext.Notifications
			.Where(notification => notification.NotificationId == notificationId
				&& notification.RecipientUserId == recipientUserId
				&& !notification.IsRead)
			.ExecuteUpdateAsync(
				setters => setters
					.SetProperty(notification => notification.IsRead, true)
					.SetProperty(notification => notification.ReadAt, DateTime.UtcNow),
				cancellationToken);

		return updated > 0;
	}

	public Task<int> MarkAllAsReadAsync(
		Guid recipientUserId,
		CancellationToken cancellationToken) =>
		_dbContext.Notifications
			.Where(notification => notification.RecipientUserId == recipientUserId
				&& !notification.IsRead)
			.ExecuteUpdateAsync(
				setters => setters
					.SetProperty(notification => notification.IsRead, true)
					.SetProperty(notification => notification.ReadAt, DateTime.UtcNow),
				cancellationToken);

	public Task<NotificationOrderTargetDTO?> GetOrderTargetAsync(
		Guid emailInvitationId,
		CancellationToken cancellationToken) =>
		_dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(order => order.EmailInvitationID == emailInvitationId)
			.Select(order => new NotificationOrderTargetDTO
			{
				EmailInvitationId = order.EmailInvitationID,
				RequestorId = order.RequestorId,
				FirstName = order.FirstName,
				LastName = order.LastName
			})
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<List<BulkEmailCompletionDTO>> GetCompletedBulkEmailFilesAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		if (emailInvitationIds.Count == 0)
		{
			return [];
		}

		var ids = emailInvitationIds.ToList();

		// The files the just-sent orders belong to. Single orders have no BulkFileID and
		// are excluded here - they get their own notification at creation time.
		var fileIds = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(order => ids.Contains(order.EmailInvitationID) && order.BulkFileID != null)
			.Select(order => order.BulkFileID!.Value)
			.Distinct()
			.ToListAsync(cancellationToken);

		if (fileIds.Count == 0)
		{
			return [];
		}

		// Counted across the whole file, not the batch: the job sends in claimed slices, so
		// a file is only finished when none of its orders are still Pending or Processing.
		var progress = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(order => order.BulkFileID != null && fileIds.Contains(order.BulkFileID.Value))
			.GroupBy(order => order.BulkFileID!.Value)
			.Select(group => new
			{
				FileId = group.Key,
				TotalCount = group.Count(),
				SentCount = group.Count(order => order.EmailSentStatus == EmailStatus.Done),
				FailedCount = group.Count(order => order.EmailSentStatus == EmailStatus.Error),
				InFlightCount = group.Count(order =>
					order.EmailSentStatus == EmailStatus.Pending
					|| order.EmailSentStatus == EmailStatus.Processing)
			})
			.Where(file => file.InFlightCount == 0)
			.ToListAsync(cancellationToken);

		if (progress.Count == 0)
		{
			return [];
		}

		var completedFileIds = progress.Select(file => file.FileId).ToList();

		// Files already announced. Without this, a file completes more than once: the package
		// follow-up chaser puts a delivered row back to Pending, and when it reaches Done a
		// second time the file is "complete" again and the uploader is told again, days after
		// they stopped caring. An operator resend on a bulk row does the same thing.
		//
		// The check lives here rather than in the service because the service has no reason to
		// know that EntityId carries the file id - and doing it in SQL means one round trip
		// instead of one per file.
		var alreadyAnnouncedFileIds = await _dbContext.Notifications
			.AsNoTracking()
			.Where(notification => notification.Type == AtsNotificationType.BulkEmailsCompleted
				&& notification.EntityId != null
				&& completedFileIds.Contains(notification.EntityId.Value))
			.Select(notification => notification.EntityId!.Value)
			.Distinct()
			.ToListAsync(cancellationToken);

		if (alreadyAnnouncedFileIds.Count > 0)
		{
			progress = progress
				.Where(file => !alreadyAnnouncedFileIds.Contains(file.FileId))
				.ToList();

			if (progress.Count == 0)
			{
				return [];
			}

			completedFileIds = progress.Select(file => file.FileId).ToList();
		}

		var files = await _dbContext.BulkUploadFileDetails
			.AsNoTracking()
			.Where(file => completedFileIds.Contains(file.FileID))
			.Select(file => new
			{
				file.FileID,
				file.FileName,
				file.UploadedByUserId
			})
			.ToListAsync(cancellationToken);

		return progress
			.Join(
				files,
				file => file.FileId,
				detail => detail.FileID,
				(file, detail) => new BulkEmailCompletionDTO
				{
					FileId = file.FileId,
					FileName = detail.FileName,
					UploadedByUserId = detail.UploadedByUserId,
					TotalCount = file.TotalCount,
					SentCount = file.SentCount,
					FailedCount = file.FailedCount
				})
			.ToList();
	}

	private IQueryable<AtsNotification> BuildRowsQuery(Guid recipientUserId, bool unreadOnly)
	{
		var query = _dbContext.Notifications
			.AsNoTracking()
			.Where(notification => notification.RecipientUserId == recipientUserId);

		if (unreadOnly)
		{
			query = query.Where(notification => !notification.IsRead);
		}

		return query;
	}

	// Newest first, unique NotificationId as the tiebreaker. ApplySeek must mirror this
	// expression exactly. Matches IX (RecipientUserId, CreatedAt DESC, NotificationId DESC).
	private static IQueryable<AtsNotification> ApplyOrder(IQueryable<AtsNotification> query) =>
		query
			.OrderByDescending(notification => notification.CreatedAt)
			.ThenByDescending(notification => notification.NotificationId);

	private static IQueryable<AtsNotification> ApplySeek(
		IQueryable<AtsNotification> query,
		DateTime afterCreatedAt,
		Guid afterNotificationId) =>
		query.Where(notification => notification.CreatedAt < afterCreatedAt
			|| (notification.CreatedAt == afterCreatedAt
				&& notification.NotificationId.CompareTo(afterNotificationId) < 0));

	private static readonly Expression<Func<AtsNotification, NotificationListDTO>> Projection =
		notification => new NotificationListDTO
		{
			NotificationId = notification.NotificationId,
			CreatedAt = notification.CreatedAt,
			Type = notification.Type,
			Title = notification.Title,
			Body = notification.Body,
			LinkUrl = notification.LinkUrl,
			EntityId = notification.EntityId,
			IsRead = notification.IsRead
		};
}
