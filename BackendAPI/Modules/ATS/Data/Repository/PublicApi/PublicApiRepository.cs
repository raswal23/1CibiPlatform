namespace ATS.Data.Repository.PublicApi;

// Not cached and not decorated: an integrating client polls these to watch an order
// move, so a cached page would report the staleness they are polling to avoid. Same
// reasoning as BulkUploadRepository and OMSTicketingRepository.
public sealed class PublicApiRepository : IPublicApiRepository
{
	private readonly ATSDBContext _dbContext;

	public PublicApiRepository(ATSDBContext dbContext) => _dbContext = dbContext;

	public async Task<PublicOrderDetailDTO?> GetOrderAsync(
		Guid orderId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var order = await ApplyOrderScope(
				_dbContext.EmailInvitationRequests.AsNoTracking(),
				authorizedClientIds,
				requiredRequestorId)
			.Where(invitation => invitation.EmailInvitationID == orderId)
			.Select(invitation => new PublicOrderDetailDTO
			{
				OrderId = invitation.EmailInvitationID,
				FirstName = invitation.FirstName,
				MiddleInitial = invitation.MiddleInitial,
				LastName = invitation.LastName,
				EmailAddress = invitation.EmailAddress,
				MobileNumber = invitation.MobileNumber,
				Package = invitation.SelectPackage,
				OrderType = invitation.RushNormal,
				OrderStatus = invitation.OrderStatus,
				ApplicationFormStatus = invitation.ApplicationFormStatus,
				TicketNumber = invitation.TicketNumber,
				TicketDeliveryDate = invitation.TicketDeliveryDate,
				OrderCreatedAt = invitation.OrderCreatedAt,
				FormCompletedAt = invitation.FormCompletedAt,
				OrderCompletedAt = invitation.OrderCompletedAt
			})
			.FirstOrDefaultAsync(cancellationToken);

		if (order is null)
		{
			return null;
		}

		// Fetched separately rather than as a correlated subquery: the timeline is a
		// second, ordered result set and this keeps the projection above flat.
		order.History = await _dbContext.OrderStatusHistories
			.AsNoTracking()
			.Where(history => history.EmailInvitationRequestId == orderId)
			.OrderBy(history => history.OccurredAt)
			.Select(history => new OrderStatusHistoryDTO(
				history.OrderStatusHistoryId,
				history.EventType,
				history.PreviousStatus,
				history.NewStatus,
				history.Source,
				history.OccurredAt))
			.ToListAsync(cancellationToken);

		return order;
	}

	public async Task<PublicBulkUploadStatusDTO?> GetBulkUploadStatusAsync(
		Guid fileId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredUploaderId,
		CancellationToken cancellationToken)
	{
		var file = await _dbContext.BulkUploadFileDetails
			.AsNoTracking()
			.Where(upload => upload.FileID == fileId)
			.Where(upload => (authorizedClientIds == null
					|| (upload.ClientId.HasValue && authorizedClientIds.Contains(upload.ClientId.Value)))
				&& (!requiredUploaderId.HasValue
					|| upload.UploadedByUserId == requiredUploaderId.Value))
			.Select(upload => new
			{
				upload.FileID,
				upload.FileName,
				upload.Status,
				upload.PackageType,
				upload.OrderType,
				upload.DateCreated,
				upload.AcceptedRowCount,
				upload.RejectedRowCount,
				upload.RejectedRows
			})
			.FirstOrDefaultAsync(cancellationToken);

		if (file is null)
		{
			return null;
		}

		// Stored as JSON because the rejected rows never became entities - they were
		// refused before insert, so there is no table to read them back from.
		var rejectedRows = string.IsNullOrWhiteSpace(file.RejectedRows)
			? []
			: JsonSerializer.Deserialize<List<BulkUploadRejectedRowDTO>>(file.RejectedRows) ?? [];

		return new PublicBulkUploadStatusDTO
		{
			FileId = file.FileID,
			FileName = file.FileName,
			Status = file.Status,
			Package = file.PackageType,
			OrderType = file.OrderType,
			DateCreated = file.DateCreated,
			AcceptedRowCount = file.AcceptedRowCount,
			RejectedRowCount = file.RejectedRowCount,
			RejectedRows = rejectedRows
		};
	}

	public async Task<bool> WithdrawOrderAsync(
		Guid orderId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		// Scope and terminal-state guards live in the UPDATE predicate, so a concurrent
		// completion or a second call updates nothing rather than racing a read.
		var updated = await ApplyOrderScope(
				_dbContext.EmailInvitationRequests,
				authorizedClientIds,
				requiredRequestorId)
			.Where(invitation => invitation.EmailInvitationID == orderId
				&& invitation.ApplicationFormStatus != ApplicationFormStatus.Withdrawn
				&& invitation.OrderStatus != OrderStatus.Completed)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.ApplicationFormStatus, x => ApplicationFormStatus.Withdrawn)
				.SetProperty(x => x.OrderStatus, x => OrderStatus.ApplicationWithdrawn)

				// The search projection is denormalized, so it has to be rebuilt with
				// the new status.
				.SetProperty(x => x.NeedsProjection, x => true),
				cancellationToken);

		return updated > 0;
	}

	public Task<string?> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken) =>
		_dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(invitation => invitation.EmailInvitationID == orderId)
			.Select(invitation => invitation.OrderStatus)
			.FirstOrDefaultAsync(cancellationToken);

	// A null client set means unrestricted (super admin); an empty set filters
	// everything out. Mirrors the rule every other ATS read applies.
	private static IQueryable<EmailInvitationRequest> ApplyOrderScope(
		IQueryable<EmailInvitationRequest> query,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId) =>
		query.Where(invitation => (authorizedClientIds == null
				|| (invitation.ClientId.HasValue && authorizedClientIds.Contains(invitation.ClientId.Value)))
			&& (!requiredRequestorId.HasValue
				|| invitation.RequestorId == requiredRequestorId.Value));
}
