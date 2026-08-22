// Scoped to this file: a module-wide System.ComponentModel import would make IContainer
// ambiguous with QuestPDF in the PDF services.
using System.ComponentModel;

namespace ATS.AI;

/// <summary>
/// The functions the ATS assistant is allowed to call. A new instance is created per
/// request and carries the caller's <see cref="ICurrentUser"/>, so every lookup is
/// limited to the clients and requests the caller is authorized for and the model can
/// never reach another client's orders.
/// </summary>
public sealed class AtsAssistantPlugin
{
	private const int MaxSearchResults = 10;

	private readonly IATSRepository _atsRepository;
	private readonly IOrderHistoryService _orderHistoryService;
	private readonly IPackageManagementService _packageManagementService;
	private readonly AtsOrderDraftStore _draftStore;
	private readonly IAtsAccessScopeResolver _accessScopeResolver;
	private readonly Guid _userId;
	private readonly int? _clientId;

	public AtsAssistantPlugin(
		IATSRepository atsRepository,
		IOrderHistoryService orderHistoryService,
		IPackageManagementService packageManagementService,
		AtsOrderDraftStore draftStore,
		ICurrentUser currentUser,
		IAtsAccessScopeResolver accessScopeResolver)
	{
		_atsRepository = atsRepository;
		_orderHistoryService = orderHistoryService;
		_packageManagementService = packageManagementService;
		_draftStore = draftStore;
		_accessScopeResolver = accessScopeResolver;
		_userId = currentUser.UserId ?? Guid.Empty;
		_clientId = currentUser.AtsClientId;
	}

	/// <summary>
	/// Orders surfaced during this turn, so the service can render them as a table.
	/// </summary>
	public List<AtsOrderSummaryDTO> LastSearchResults { get; } = new();

	/// <summary>
	/// The order staged during this turn, if any, so the service can render a confirm card.
	/// </summary>
	public AtsOrderDraftDTO? StagedDraft { get; private set; }

	[KernelFunction]
	[Description("Search background check orders by the candidate or subject name. Use this to "
		+ "answer any question about the status, package, requestor or result of a person's order.")]
	public async Task<IReadOnlyList<AtsOrderSummaryDTO>> SearchOrdersBySubjectAsync(
		[Description("Full or partial candidate name, for example 'Russel Gutierrez'.")]
		string name,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return Array.Empty<AtsOrderSummaryDTO>();
		}

		var scope = await ResolveReportScopeAsync(cancellationToken);

		if (scope is null)
		{
			return Array.Empty<AtsOrderSummaryDTO>();
		}

		var reports = await _atsRepository.SearchReportsPageAsync(
			afterRank: null,
			afterCompletedAt: null,
			afterId: null,
			take: MaxSearchResults,
			searchTerm: name.Trim(),
			startDate: null,
			endDate: null,
			scope.Value.AuthorizedClientIds,
			scope.Value.RequiredRequestorId,
			cancellationToken);

		var orders = reports
			.Select(report => new AtsOrderSummaryDTO
			{
				EmailInvitationRequestId = report.EmailInvitationID,
				SubjectName = $"{report.FirstName} {report.LastName}".Trim(),
				OrderStatus = report.OrderStatus,
				SelectedPackage = report.SelectPackage,
				Requestor = report.Requestor,
				HitStatus = report.HitStatus,
				OrderCompletedAt = report.OrderCompletedAt
			})
			.ToArray();

		LastSearchResults.Clear();
		LastSearchResults.AddRange(orders);

		return orders;
	}

	[KernelFunction]
	[Description("Get the dated status timeline of a single order. Only call this with an order id "
		+ "returned by SearchOrdersBySubject.")]
	public async Task<IReadOnlyList<OrderStatusHistoryDTO>> GetOrderStatusHistoryAsync(
		[Description("The EmailInvitationRequestId returned by SearchOrdersBySubject.")]
		Guid orderId,
		CancellationToken cancellationToken)
	{
		if (orderId == Guid.Empty)
		{
			return Array.Empty<OrderStatusHistoryDTO>();
		}

		return await _orderHistoryService.GetAsync(orderId, cancellationToken);
	}

	[KernelFunction]
	[Description("List the screening packages available to the current user's client. Always call "
		+ "this before staging a new order, and only use a package name that it returns.")]
	public async Task<IReadOnlyList<string>> GetAvailablePackagesAsync(
		CancellationToken cancellationToken)
	{
		var packages = await GetAssignedPackagesAsync(cancellationToken);

		return packages
			.Select(package => package.PackageName)
			.ToArray();
	}

	[KernelFunction]
	[Description("Call this to prepare a new background check order. You MUST call this "
		+ "function whenever the user wants to create or endorse an order and you have all the "
		+ "required details. Simply describing the order in your reply does not prepare it. "
		+ "This does NOT create the order and does NOT email the candidate; it only prepares a "
		+ "draft that the application shows the user for confirmation.")]
	public async Task<string> StageNewOrderAsync(
		[Description("Candidate first name.")]
		string firstName,
		[Description("Candidate last name.")]
		string lastName,
		[Description("Candidate email address.")]
		string emailAddress,
		[Description("Philippine mobile number, exactly 11 digits, for example 09171234567.")]
		string mobileNumber,
		[Description("Screening package name. Must be one returned by GetAvailablePackagesAsync.")]
		string selectPackage,
		[Description("Processing speed. Either 'Normal' or 'Rush'.")]
		string rushNormal,
		[Description("Optional candidate middle initial.")]
		string? middleInitial = null,
		CancellationToken cancellationToken = default)
	{
		var validationError = ValidateDraft(
			firstName,
			lastName,
			emailAddress,
			mobileNumber,
			rushNormal);

		if (validationError is not null)
		{
			return validationError;
		}

		var packages = await GetAssignedPackagesAsync(cancellationToken);

		if (packages.Count == 0)
		{
			return "No screening package is assigned to this client, so an order cannot be created.";
		}

		var matchedPackage = packages
			.FirstOrDefault(package => string.Equals(
				package.PackageName,
				selectPackage?.Trim(),
				StringComparison.OrdinalIgnoreCase));

		if (matchedPackage is null)
		{
			var available = string.Join(", ", packages.Select(package => package.PackageName));

			return $"'{selectPackage}' is not a package available to this client. "
				+ $"Ask the user to choose one of: {available}.";
		}

		var normalizedSpeed = string.Equals(rushNormal?.Trim(), "Rush", StringComparison.OrdinalIgnoreCase)
			? "Rush"
			: "Normal";

		StagedDraft = _draftStore.Stage(
			_userId,
			new AtsOrderDraftDTO
			{
				FirstName = firstName.Trim(),
				LastName = lastName.Trim(),
				MiddleInitial = string.IsNullOrWhiteSpace(middleInitial) ? null : middleInitial.Trim(),
				EmailAddress = emailAddress.Trim(),
				MobileNumber = mobileNumber.Trim(),
				SelectPackage = matchedPackage.PackageName,
				RushNormal = normalizedSpeed
			});

		return "The draft is prepared and the application is now showing it to the user. "
			+ "The order has NOT been created yet. Reply briefly and do not describe a card "
			+ "or ask the user to press anything.";
	}

	// Delegates to AtsAccessScopeResolver - this used to be a fourth inline copy of the
	// role ladder. The assistant must never see further than the user it answers for.
	private async Task<(IReadOnlyCollection<int>? AuthorizedClientIds, Guid? RequiredRequestorId)?>
		ResolveReportScopeAsync(CancellationToken cancellationToken)
	{
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			return null;
		}

		return (scope.AuthorizedClientIds, scope.RequiredOwnerId);
	}

	private async Task<IReadOnlyList<PackageDetailsDTO>> GetAssignedPackagesAsync(
		CancellationToken cancellationToken)
	{
		var paginationRequest = new KeysetPaginationRequest(
			Cursor: null,
			PageSize: 100);

		var packages = await _packageManagementService.GetPackagesAsync(
			paginationRequest,
			cancellationToken,
			_clientId);

		return packages.Items
			.Where(package => package.IsActive)
			.DistinctBy(package => package.PackageId)
			.OrderBy(package => package.PackageName)
			.ToArray();
	}

	private static string? ValidateDraft(
		string firstName,
		string lastName,
		string emailAddress,
		string mobileNumber,
		string rushNormal)
	{
		if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 50)
		{
			return "A first name is required and must not exceed 50 characters.";
		}

		if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 50)
		{
			return "A last name is required and must not exceed 50 characters.";
		}

		if (string.IsNullOrWhiteSpace(emailAddress) || !IsValidEmail(emailAddress.Trim()))
		{
			return "A valid candidate email address is required.";
		}

		if (!Regex.IsMatch(mobileNumber?.Trim() ?? string.Empty, @"^\d{11}$"))
		{
			return "The mobile number must be exactly 11 digits, for example 09171234567.";
		}

		if (string.IsNullOrWhiteSpace(rushNormal))
		{
			return "Processing speed is required. Ask the user for either 'Normal' or 'Rush'.";
		}

		return null;
	}

	private static bool IsValidEmail(string email)
	{
		try
		{
			return new MailAddress(email).Address == email;
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
