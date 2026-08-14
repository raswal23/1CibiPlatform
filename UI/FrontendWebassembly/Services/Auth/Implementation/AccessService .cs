namespace FrontendWebassembly.Services.Auth.Implementation;

using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

public class AccessService : IAccessService
{
	private readonly LocalStorageService _localStorage;
	private readonly ILogger<AccessService> _logger;
	private const string _appIdKey = "AppId";
	private const string _subMenuKey = "SubMenuId";
	private const string _roleIdKey = "RoleId";

	public AccessService(LocalStorageService localStorage, ILogger<AccessService> logger)
	{
		_localStorage = localStorage;
		_logger = logger;
	}


	public async Task<bool> HasAccessAsync(int appId, int subMenuId)
	{
		var apps = await GetStoredValueAsync<List<int>>(_appIdKey);
		var subMenus = await GetStoredValueAsync<List<List<int>>>(_subMenuKey);

		_logger.LogDebug("Apps: {Apps}", string.Join(", ", apps ?? new List<int>()));
		_logger.LogDebug("SubMenus: {SubMenus}", string.Join(", ", subMenus?.SelectMany(sm => sm) ?? new List<int>()));

		if (apps is null || subMenus is null)
		{
			return false;
		}

		if (!apps.Contains(appId))
		{
			_logger.LogWarning("AppId {AppId} not found in user's apps.", appId);
			return false;
		}

		var index = apps.IndexOf(appId);
		_logger.LogDebug("Index of AppId {AppId}: {Index}", appId, index);

		if (index < 0 || index >= subMenus.Count || !subMenus[index].Contains(subMenuId))
		{
			_logger.LogWarning("SubMenuId {SubMenuId} not found for AppId {AppId}.", subMenuId, appId);
			return false;
		}


		return true;
	}

	public async Task<bool> HasRoleAsync(int roleId)
	{
		if (roleId <= 0)
			return false;

		var roleIds = await GetStoredValueAsync<List<int>>(_roleIdKey);
		return roleIds?.Contains(roleId) == true;
	}

	private async Task<T?> GetStoredValueAsync<T>(string key)
	{
		var json = await _localStorage.GetItemAsync<string>(key);
		if (string.IsNullOrWhiteSpace(json))
			return default;

		try
		{
			return JsonSerializer.Deserialize<T>(json);
		}
		catch (JsonException exception)
		{
			_logger.LogWarning(exception, "Ignoring malformed local-storage value for {StorageKey}", key);
			return default;
		}
	}

}
