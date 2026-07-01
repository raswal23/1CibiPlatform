namespace FrontendWebassembly.Layout;

public partial class MainLayout
{
	private readonly Dictionary<int, int> AppOrder = new()
	{
		{ 5, 1 }, // Credit Bureau
        { 6, 2 }, // S&I
        { 2, 3 }, // PhilSys
        { 1, 4 }, // CNX
        { 4, 5 }, // AI
        { 3, int.MaxValue } // Settings
    };

	private bool _drawerOpen = true;
	private bool _isDarkMode = false;
	private bool _isLoading = true;
	private string name = "";

	private const string _appIdKey = "AppId";
	private const string _subMenuKey = "SubMenuId";
	private const string _roleIdKey = "RoleId";
	private const string _userNameKey = "Name";

	private List<int> Apps = new List<int>();
	private List<List<int>> SubMenus = new List<List<int>>();
	private List<int> Roles = new List<int>();

	private string GetContainerStyle()
	{
		var background = !_isDarkMode
			? "white"
			: "linear-gradient(90deg, #102247 0%, #2a77ae 50%)";

		return $"display:flex;justify-content:center;align-items:center;background:{background} !important;";
	}

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
			DefaultBorderRadius = "4px",
			DrawerWidthLeft = "260px",
			AppbarHeight = "69px"
		}
	};

	private void DrawerToggle() => _drawerOpen = !_drawerOpen;
	private async Task ToggleDarkMode()
	{
		_isDarkMode = !_isDarkMode;
		await LocalStorageService.SetItemAsync("isDarkMode", _isDarkMode);
		await JS.InvokeVoidAsync("setStartupTheme", _isDarkMode);

	}

	private string GetAppBarStyle()
	{
		var gradient = _isDarkMode
			? "linear-gradient(90deg, #68c0d6 0%, #2a77ae 50%, #102247 100%)"
			: "linear-gradient(90deg, #102247 0%, #2a77ae 50%, #68c0d6 100%)";

		// dynamically adjust margin-left if drawer is open


		return $@"
        width: auto !important;
        background: {gradient} !important;
		border-radius: 4px;
        transition: margin-left 0.3s ease, margin-right 0.3s ease;
    ";
	}

	private string GetNavLinkStyle(bool isActive)
	{
		if (isActive)
			return _isDarkMode
				? "background: white; color: black;"
				: "background: linear-gradient(90deg, #102247 0%, #2a77ae 50%); color: white;";

		return "";
	}

	private string GetMenuIconStyle()
	{
		return _isDarkMode
			? "color: #102247;"
			: "color: white;";
	}
	protected override async Task OnInitializedAsync()
	{
		try
		{
			var isAuthenticated = await IAuthService.IsAuthenticated();

			if (!isAuthenticated)
			{
				var isDarkMode = await LocalStorageService.GetItemAsync<bool?>("isDarkMode");
				await LocalStorageService.ClearAsync();
				if (isDarkMode.HasValue)
				{
					await LocalStorageService.SetItemAsync("isDarkMode", isDarkMode.Value);
				}

				Navigation.NavigateTo("/login");

				return;
			}

			Apps = JsonSerializer.Deserialize<List<int>>(await LocalStorageService.GetItemAsync<string>(_appIdKey));
			Console.WriteLine($"Apps: {string.Join(", ", Apps)}");

			SubMenus = JsonSerializer.Deserialize<List<List<int>>>(await LocalStorageService.GetItemAsync<string>(_subMenuKey));
			Console.WriteLine($"SubMenus: {string.Join(", ", SubMenus.SelectMany(sm => sm))}");

			Roles = JsonSerializer.Deserialize<List<int>>(await LocalStorageService.GetItemAsync<string>(_roleIdKey));
			Console.WriteLine($"Roles: {string.Join(", ", Roles)}");

			name = await LocalStorageService.GetItemAsync<string>(_userNameKey) ?? string.Empty;

			var stored = await LocalStorageService.GetItemAsync<bool?>("isDarkMode");

			_isDarkMode = stored ?? false;

			await JS.InvokeVoidAsync("setStartupTheme", _isDarkMode);

			_isLoading = false;
		}
		catch (Exception ex)
		{
			_isLoading = false;
			Console.WriteLine($"Is loading: {_isLoading}");
			Console.WriteLine($"Authentication Error: {ex.Message}");
			throw;
		}
	}

	private string GetActiveNavClass()
	{
		return _isDarkMode ? "nav-active-light" : "nav-active-dark";
	}

	private async Task HandleLogout()
	{
		Console.WriteLine("Logging out...");

		try
		{
			var logout = await IAuthService.Logout();


			if (logout)
			{
				Console.WriteLine(logout ? "Logout successful." : "Logout failed.");
				Navigation.NavigateTo("/login");

				return;
			}
		}
		catch (Exception ex)
		{
			_isLoading = true;
			Console.WriteLine($"Authentication Error: {ex.Message}");
			throw;
		}
	}
}
