namespace FrontendWebassembly.Layout;

public partial class SSOLayout
{
	private bool _drawerOpen = true;
	private bool _isDarkMode = false;
	private bool _isLoading = true;

	private const string _appIdKey = "AppId";
	private const string _subMenuKey = "SubMenuId";
	private const string _roleIdKey = "RoleId";

	private List<int> Apps = new List<int>();
	private List<List<int>> SubMenus = new List<List<int>>();
	private List<int> Roles = new List<int>();


	private MudTheme _myTheme = new MudTheme()
	{
		PaletteLight = new PaletteLight()
		{
			Primary = "#667eea",
			Secondary = "#764ba2",
			Background = Colors.Gray.Lighten5,
			Surface = Colors.Shades.White,
			AppbarBackground = "#667eea",
			AppbarText = Colors.Shades.White,
			TextPrimary = Colors.Gray.Darken3
		},
		PaletteDark = new PaletteDark()
		{
			Primary = "#8b9dff",
			Secondary = "#9d6bc7",
			Background = Colors.Gray.Darken4,
			Surface = Colors.Gray.Darken3,
			AppbarBackground = "#5568d3",
			AppbarText = Colors.Shades.White,
			TextPrimary = Colors.Shades.White
		},
		LayoutProperties = new LayoutProperties()
		{
			DefaultBorderRadius = "10px",
			DrawerWidthLeft = "260px",
			DrawerWidthRight = "260px",
			AppbarHeight = "64px"
		}
	};

	private void DrawerToggle() => _drawerOpen = !_drawerOpen;
	private async Task ToggleDarkMode()
	{
		_isDarkMode = !_isDarkMode;
		await LocalStorageService.SetItemAsync("isDarkMode", _isDarkMode);
	}

	private string GetAppBarStyle()
	{
		return _isDarkMode
			? "background: linear-gradient(90deg, #68c0d6 0%, #2a77ae 50%, #102247 100%) !important;"
			: "background: linear-gradient(90deg, #102247 0%, #2a77ae 50%, #68c0d6 100%) !important;";
	}
	private string GetMenuIconStyle()
	{
		return _isDarkMode
			? "color: #102247;"
			: "color: white;";
	}

	private async Task LogoutAsync()
	{
		try
		{
			var success = await SSOService.LogoutAsync();
			await LocalStorageService.ClearAsync();
			await JS.InvokeVoidAsync("location.reload");

		}
		catch (Exception ex)
		{
		}
	}
}
