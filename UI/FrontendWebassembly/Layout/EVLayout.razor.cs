namespace FrontendWebassembly.Layout;

public partial class EVLayout
{
	[Inject] private NavigationManager NavigationManager { get; set; } = default!;
	[Inject] private IAuthService IAuthService { get; set; } = default!;
	[Inject] private LocalStorageService LocalStorageService { get; set; } = default!;
	[Inject] private ThemeService Theme { get; set; } = default!;

	private const string UserNameStorageKey = "Name";

	/// <summary>
	/// The navbar tabs, in display order. Declared here rather than in markup so the
	/// route strings have one home; <see cref="EmploymentVerificationRoutes"/> is what
	/// the pages themselves use, so a renamed route cannot drift from its tab.
	/// </summary>
	private static readonly EVTab[] Tabs =
	[
		new(EmploymentVerificationRoutes.NeedsRequest, "Needs request", Icons.Material.Filled.PersonSearch),
		new(EmploymentVerificationRoutes.Tracking, "Tracking", Icons.Material.Filled.FactCheck),
		new(EmploymentVerificationRoutes.Contacts, "Contacts", Icons.Material.Filled.ContactMail)
	];

	private bool _isReady;
	private string _userDisplayName = "User";
	private string _userInitials = "U";

	protected override async Task OnInitializedAsync()
	{
		if (!await IAuthService.IsAuthenticated())
		{
			NavigationManager.NavigateTo("/login", true);
			return;
		}

		var storedUserName = await LocalStorageService.GetItemAsync<string>(UserNameStorageKey);
		_userDisplayName = string.IsNullOrWhiteSpace(storedUserName)
			? "User"
			: storedUserName.Trim();
		_userInitials = GetUserInitials(_userDisplayName);

		// Per-page permission is enforced by SecurePageBase via [RequirePermission(8, 9)];
		// the layout only needs the session and the identity shown in the topbar.
		Theme.OnChanged += HandleThemeChanged;
		_isReady = true;
	}

	private async Task ToggleThemeAsync() => await Theme.ToggleAsync();

	private void HandleThemeChanged() => InvokeAsync(StateHasChanged);

	private void GoToOnePlatform() => NavigationManager.NavigateTo("/");

	private static string GetUserInitials(string? fullName)
	{
		var nameParts = fullName?
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			?? [];

		if (nameParts.Length == 0)
		{
			return "U";
		}

		var firstInitial = char.ToUpperInvariant(nameParts[0][0]);

		if (nameParts.Length == 1)
		{
			return firstInitial.ToString();
		}

		var lastInitial = char.ToUpperInvariant(nameParts[^1][0]);

		return $"{firstInitial}{lastInitial}";
	}

	public void Dispose()
	{
		Theme.OnChanged -= HandleThemeChanged;
	}

	private sealed record EVTab(string Href, string Label, string Icon);
}
