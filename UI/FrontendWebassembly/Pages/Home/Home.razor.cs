namespace FrontendWebassembly.Pages.Home;

using System.Globalization;
using System.Text.Json;
using FrontendWebassembly.ShareData.Auth;
using Microsoft.AspNetCore.Components;

public partial class Home
{
	[Inject] private LocalStorageService LocalStorageService { get; set; } = default!;
	[CascadingParameter(Name = "ApplicationSearchQuery")]
	private string ApplicationSearchQuery { get; set; } = string.Empty;

	private const string UserNameKey = "Name";
	private const string AppIdKey = "AppId";
	private const string SubMenuKey = "SubMenuId";

	private readonly Dictionary<int, int> AppOrder = new()
	{
		{ 6, 1 },
		{ 2, 2 },
		{ 7, 3 },
		{ 4, 4 },
		{ 3, 5 }
	};

	private readonly List<string> AccentGradients =
	[
		"linear-gradient(135deg, #3b7bf6, #1f4fc4)",
		"linear-gradient(135deg, #14b8a6, #0d7d70)",
		"linear-gradient(135deg, #6366f1, #3d34c9)",
		"linear-gradient(135deg, #10b981, #04795a)",
		"linear-gradient(135deg, #64748b, #3a4657)",
		"linear-gradient(135deg, #36506c, #6f8ba7)"
	];

	private bool isLoading = true;
	private string DisplayName = "User";
	private string CurrentDateText => DateTime.Now.ToString("dddd, MMM d, yyyy", CultureInfo.InvariantCulture);

	private List<int> UserAppIds = new();
	private List<List<int>> UserSubMenus = new();

	private List<HomeAppCard> AvailableApps = new();
	private IReadOnlyList<HomeAppCard> FilteredApps => string.IsNullOrWhiteSpace(ApplicationSearchQuery)
		? AvailableApps.Where(app=> app.AppId != 1).ToList()
		: AvailableApps
			.Where(app =>
				(app.Name.Contains(ApplicationSearchQuery.Trim(), StringComparison.OrdinalIgnoreCase) ||
				app.Subtitle.Contains(ApplicationSearchQuery.Trim(), StringComparison.OrdinalIgnoreCase)) &&
				app.AppId != 1)
			.ToList();

	private readonly List<AnnouncementItem> Announcements =
	[
		new("Update", "Case filing redesign rollout", "Faster filing with cleaner category selection. (Editorial placeholder)"),
		new("Reminder", "Quarterly password rotation", "Password rotation notices will appear here once connected to a real announcement source.")
	];

	protected override async Task OnInitializedAsync()
	{
		var name = await LocalStorageService.GetItemAsync<string>(UserNameKey);
		DisplayName = string.IsNullOrWhiteSpace(name) ? "User" : name;

		var appIdsJson = await LocalStorageService.GetItemAsync<string>(AppIdKey);
		UserAppIds = string.IsNullOrWhiteSpace(appIdsJson)
			? new List<int>()
			: JsonSerializer.Deserialize<List<int>>(appIdsJson) ?? new List<int>();

		var subMenusJson = await LocalStorageService.GetItemAsync<string>(SubMenuKey);
		UserSubMenus = string.IsNullOrWhiteSpace(subMenusJson)
			? new List<List<int>>()
			: JsonSerializer.Deserialize<List<List<int>>>(subMenusJson) ?? new List<List<int>>();

		BuildAvailableApps();
		isLoading = false;
	}

	private void BuildAvailableApps()
	{
		var permissionMap = UserAppIds
			.Select((appId, index) => new
			{
				AppId = appId,
				SubMenus = index < UserSubMenus.Count ? UserSubMenus[index] ?? new List<int>() : new List<int>()
			})
			.GroupBy(x => x.AppId)
			.ToDictionary(
				group => group.Key,
				group => group.SelectMany(item => item.SubMenus).Distinct().ToList());

		AvailableApps = ApplicationListDescriptionIcon.List
			.OrderBy(entry => AppOrder.TryGetValue(entry.Key, out var order) ? order : 100)
			// Keep the dashboard focused on the five applications represented in the
			// unified-console design. Other IDs remain available on their own routes.
			.Where(entry => permissionMap.ContainsKey(entry.Key))
			.Select((entry, index) =>
			{
				var appId = entry.Key;
				var (path, name, icon, subtitle) = entry.Value;
				var openRoute = BuildOpenRoute(path, permissionMap[appId]);
				var accent = AccentGradients[index % AccentGradients.Count];
				var resolvedIcon = appId switch
				{
					2 => Icons.Material.Filled.Badge,
					4 => Icons.Material.Filled.VerifiedUser,
					_ when string.IsNullOrWhiteSpace(icon) => Icons.Material.Filled.Apps,
					_ => icon
				};
				var displayName = name;

				return new HomeAppCard(appId, path, displayName, subtitle, resolvedIcon, openRoute, accent);
			})
			.ToList();
	}

	private string BuildOpenRoute(string path, IReadOnlyList<int> permittedSubMenus)
	{
		var normalizedPath = path.ToLowerInvariant();
		var fallback = $"/{normalizedPath}";

		var firstSubMenuId = permittedSubMenus.FirstOrDefault();
		if (firstSubMenuId == 0 || !SubMenuList.List.TryGetValue(firstSubMenuId, out var subMenu))
		{
			return fallback;
		}

		var (subPath, subDescription, _) = subMenu;
		if (subDescription == "General")
		{
			return $"/{normalizedPath}/general/personal-settings";
		}

		if (subDescription == "Security Control")
		{
			return $"/{normalizedPath}/securitycontrol/profiles";
		}

		return $"/{normalizedPath}/{subPath.ToLowerInvariant()}";
	}

	private sealed record HomeAppCard(int AppId, string Path, string Name, string Subtitle, string Icon, string OpenRoute, string AccentGradient);
	private sealed record AnnouncementItem(string Tag, string Title, string Detail);
}
