namespace FrontendWebassembly.Services.ATS.Implementation;

public class DownloadBulkTemplateService : IDownloadBulkTemplateService
{
	private readonly HttpClient _httpClient;

	public DownloadBulkTemplateService(IHttpClientFactory httpClientFactory)
	{
		_httpClient = httpClientFactory.CreateClient("API");
	}

	public async Task<Stream> DownloadBulkTemplateAsync()
	{
		var response = await _httpClient.GetAsync("ats/downloadBulkTemplate");
		response.EnsureSuccessStatusCode();
		var stream = await response.Content.ReadAsStreamAsync();
		return stream;
	}

	void AddFile(byte[]? file, string name)
	{
		if (file != null)
		{
			var stream = new MemoryStream(file);
			var fileContent = new StreamContent(stream);
			fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
			content.Add(fileContent, name, name);
		}
	}
}
