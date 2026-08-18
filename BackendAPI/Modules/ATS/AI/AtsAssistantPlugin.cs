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
	private readonly ICurrentUser _currentUser;
	private readonly IUserClientRepository _userClientRepository;
	private readonly Guid _userId;
	private readonly int? _clientId;

	public AtsAssistantPlugin(
		IATSRepository atsRepository,
		IOrderHistoryService orderHistoryService,
		IPackageManagementService packageManagementService,
		AtsOrderDraftStore draftStore,
		ICurrentUser currentUser,
		IUserClientRepository userClientRepository)
	{
		_atsRepository = atsRepository;
		_orderHistoryService = orderHistoryService;
		_packageManagementService = packageManagementService;
		_draftStore = draftStore;
		_currentUser = currentUser;
		_userClientRepository = userClientRepository;
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

		var paginationRequest = new PaginationRequest(
			PageIndex: 1,
			PageSize: MaxSearchResults,
			SearchTerm: name.Trim());

		var reports = await _atsRepository.SearchReportsAsync(
			paginationRequest,
			SortColumn.SubjectName,
			sortDescending: false,
			scope.Value.AuthorizedClientIds,
			scope.Value.RequiredRequestorId,
			cancellationToken);

		var orders = reports.Data
			.Select(report => new AtsOrderSummaryDTO
			{
				EmailInvitationRequestId = report.EmailInvitationRequestId,
				SubjectName = report.SubjectName,
				OrderStatus = report.OrderStatus,
				SelectedPackage = report.SelectedPackage,
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
	[Description("Stage a new background check order so the user can review and confirm it. "
		+ "This does NOT create the order and does NOT email the candidate. It only prepares a "
		+ "draft. Tell the user to review the confirmation card and press Confirm.")]
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
		CancellationToken cancellationToken,
		[Description("Optional candidate middle initial.")]
		string? middleInitial = null)
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

		return "The order draft is ready and is shown to the user as a confirmation card. "
			+ "The order has NOT been created yet. Ask the user to review it and press Confirm.";
	}

	// Mirrors the authorization block in ReportService.GetReportsAsync: null means the
	// caller may not read reports at all, (null, null) means unrestricted (super admin).
	private async Task<(IReadOnlyCollection<int>? AuthorizedClientIds, Guid? RequiredRequestorId)?>
		ResolveReportScopeAsync(CancellationToken cancellationToken)
	{
		if (!_currentUser.IsAuthenticated
			|| _currentUser.UserId is not { } userId
			|| userId == Guid.Empty)
		{
			return null;
		}

		if (_currentUser.IsPlatformSuperAdmin)
		{
			return (null, null);
		}

		if (_currentUser.AtsRoleId is not { } roleId)
		{
			return null;
		}

		if (roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin)
		{
			var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
				[userId],
				cancellationToken);

			var clientIds = assignments
				.Select(assignment => assignment.ClientId)
				.Distinct()
				.ToArray();

			return (clientIds, null);
		}

		if (roleId is AtsRoleIds.User or AtsRoleIds.Uploader
			&& _currentUser.AtsClientId is { } clientId)
		{
			return (new[] { clientId }, userId);
		}

		return null;
	}

	private async Task<IReadOnlyList<PackageDetailsDTO>> GetAssignedPackagesAsync(
		CancellationToken cancellationToken)
	{
		var paginationRequest = new PaginationRequest(
			PageIndex: 1,
			PageSize: 100);

		var packages = await _packageManagementService.GetPackagesAsync(
			paginationRequest,
			cancellationToken,
			_clientId);

		return packages.Data
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
