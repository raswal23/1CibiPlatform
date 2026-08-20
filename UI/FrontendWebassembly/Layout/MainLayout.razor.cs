namespace FrontendWebassembly.Layout;

public partial class MainLayout
{
	private bool _isDarkMode = false;
	private bool _isLoading = true;
	private string name = "";
	private string _applicationSearchQuery = string.Empty;

	private bool IsHomeRoute
	{
		get
		{
			var path = NavigationManager.ToBaseRelativePath(NavigationManager.Uri)
				.Split('?', '#')[0]
				.Trim('/');

			return string.IsNullOrEmpty(path) ||
				string.Equals(path, "dashboard", StringComparison.OrdinalIgnoreCase);
		}
	}

	private const string _userNameKey = "Name";
	private string UserInitials
	{
		get
		{
			var initials = name
				.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Take(2)
				.Select(part => char.ToUpperInvariant(part[0]));

			var value = string.Concat(initials);
			return string.IsNullOrEmpty(value) ? "U" : value;
		}
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
			AppbarHeight = "64px"
		}
	};

	private async Task ToggleDarkMode()
	{
		_isDarkMode = !_isDarkMode;
		await LocalStorageService.SetItemAsync("isDarkMode", _isDarkMode);
		await JS.InvokeVoidAsync("setStartupTheme", _isDarkMode);

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

			name = await LocalStorageService.GetItemAsync<string>(_userNameKey) ?? string.Empty;

			var stored = await LocalStorageService.GetItemAsync<bool?>("isDarkMode");

			_isDarkMode = stored ?? false;

			await JS.InvokeVoidAsync("setStartupTheme", _isDarkMode);

			NavigationManager.LocationChanged += HandleLocationChanged;
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

	private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
	{
		if (!IsHomeRoute)
		{
			_applicationSearchQuery = string.Empty;
		}

		InvokeAsync(StateHasChanged);
	}

	private void HandleApplicationSearchKeyDown(KeyboardEventArgs args)
	{
		if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
		{
			_applicationSearchQuery = string.Empty;
		}
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

	public void Dispose()
	{
		NavigationManager.LocationChanged -= HandleLocationChanged;
	}

}
