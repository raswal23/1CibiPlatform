namespace Test.BackendAPI.Infrastructure;

public static class DotEnvLoader
{
	public static void Load(string path)
	{
		if (!File.Exists(path))
			return;

		foreach (var rawLine in File.ReadAllLines(path))
		{
			var line = rawLine.Trim();

			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
				continue;

			var separatorIndex = line.IndexOf('=');
			if (separatorIndex <= 0)
				continue;

			var key = line[..separatorIndex].Trim();
			var value = line[(separatorIndex + 1)..].Trim().Trim('"');

			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
				Environment.SetEnvironmentVariable(key, value);
		}
	}
}