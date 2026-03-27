namespace FrontendWebassembly.ShareData.Auth;

public static class ApplicationListDescriptionIcon
{
	public static Dictionary<int, (string path, string Name, string Icon)> List => new()
	{
		{ 1, ("cnx","CNX", Icons.Material.Filled.Phone) },
		{ 2, ("philsys","PhilSys", Icons.Material.Filled.Flag) },
		{ 3, ("settings", "Settings", Icons.Material.Filled.Settings) },
		{ 4, ("ai", "AI", Icons.Material.Filled.Android) },
		{ 5, ("creditbureau", "Credit Bureau", Icons.Material.Filled.AccountBalance) },
		{ 6, ("s&i", "S&I", Icons.Material.Filled.Apps) }
	};

}