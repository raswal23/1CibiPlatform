namespace ATS.Services.FilePDFService;

/// <summary>
/// Raster brand assets embedded in the assembly so PDF generation never depends
/// on the web root or the filesystem. The CIBI hexagon is the same mark the
/// frontend serves as images/generic/cibi-icon.png.
/// </summary>
public static class PdfBrandAssets
{
	public static readonly byte[] CibiIcon = LoadResource("cibi-icon.png");

	private static byte[] LoadResource(string fileName)
	{
		var assembly = typeof(PdfBrandAssets).Assembly;
		var resourceName = assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}
}
