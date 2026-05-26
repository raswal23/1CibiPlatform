using Aliyun.OSS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Storage;

public static class AlibabaStorageConfigService
{

	public static IServiceCollection AddAlibabaStorage(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var ossSettings = configuration.GetSection("AlibabaOss");
		var endpoint = ossSettings["Endpoint"];
		var accessKeyId = ossSettings["AccessKeyId"];
		var accessKeySecret = ossSettings["AccessKeySecret"];


		services.AddSingleton(new OssClient(endpoint, accessKeyId, accessKeySecret));

		services.AddScoped<IObjectStorageService, AlibabaOssStorageService>();

		return services;
	}

}
