namespace FrontendWebassembly.Pages.Home;

using System.Globalization;
using System.Text.Json;
using FrontendWebassembly.ShareData.Auth;
using Microsoft.AspNetCore.Components;

public partial class Home
{
	[Inject] private LocalStorageService LocalStorageService { get; set; } = default!;

	private const string UserNameKey = "Name";
	private const string AppIdKey = "AppId";
	private const string SubMenuKey = "SubMenuId";

	private readonly Dictionary<int, int> AppOrder = new()
	{
		{ 5, 1 },
		{ 6, 2 },
		{ 2, 3 },
		{ 1, 4 },
		{ 4, 5 },
		{ 3, int.MaxValue }
	};

	private readonly List<string> AccentGradients =
	[
		"linear-gradient(135deg, #0b1f3a, #2c7fb8)",
		"linear-gradient(135deg, #1b6fa8, #7fc4e8)",
		"linear-gradient(135deg, #2c7fb8, #5fa8d3)",
		"linear-gradient(135deg, #4a5a70, #8494a8)",
		"linear-gradient(135deg, #15345f, #3a8bc9)",
		"linear-gradient(135deg, #36506c, #6f8ba7)"
	];

	private bool isLoading = true;
	private string DisplayName = "User";
	private string CurrentDateText => DateTime.Now.ToString("dddd, MMM d, yyyy", CultureInfo.InvariantCulture);

	private List<int> UserAppIds = new();
	private List<List<int>> UserSubMenus = new();

	private List<HomeAppCard> AvailableApps = new();

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
			.Where(entry => permissionMap.ContainsKey(entry.Key))
			.Select((entry, index) =>
			{
				var appId = entry.Key;
				var (path, name, icon) = entry.Value;
				var openRoute = BuildOpenRoute(path, permissionMap[appId]);
				var accent = AccentGradients[index % AccentGradients.Count];
				var resolvedIcon = string.IsNullOrWhiteSpace(icon) ? Icons.Material.Filled.Apps : icon;
				var subtitle = GetAppSubtitle(appId);

				return new HomeAppCard(appId, path, name, subtitle, resolvedIcon, openRoute, accent);
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

	private static string GetAppSubtitle(int appId) => appId switch
	{
		6 => "Screening & investigation console — manage candidate orders, reports, and disputes.",
		2 => "Look up and verify registrants using PSA national ID data.",
		1 => "Connect and manage outbound communications and call workflows.",
		3 => "Manage users, roles, permissions, and platform-wide preferences.",
		4 => "Use AI-assisted chat workflows for policy and document tasks.",
		5 => "Access credit bureau tools, reports, and account-level checks.",
		_ => "Open this application to continue your work."
	};

	private sealed record HomeAppCard(int AppId, string Path, string Name, string Subtitle, string Icon, string OpenRoute, string AccentGradient);
	private sealed record AnnouncementItem(string Tag, string Title, string Detail);
}
