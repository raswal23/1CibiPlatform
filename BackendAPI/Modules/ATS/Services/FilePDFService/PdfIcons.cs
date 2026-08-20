namespace ATS.Services.FilePDFService;

// All glyphs are white so they read correctly on the navy/blue badge backgrounds,
// except Lock which sits directly on the page and uses the muted text color.
public static class PdfIcons
{
	public const string Shield = """
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
    <path fill="#FFFFFF"
          d="M12 2L4 5v6c0 5.55 3.84 10.74 8 12
             4.16-1.26 8-6.45 8-12V5l-8-3z"/>
</svg>
""";

	public const string Document = """
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
    <path fill="#FFFFFF"
          d="M6 2h9l5 5v15a2 2 0 0 1-2 2H6
             a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zm8
             1.5V8h4.5"/>
</svg>
""";

	public const string Info = """
<svg xmlns="http://www.w3.org/2000/svg"
     viewBox="0 0 24 24">
    <path fill="#FFFFFF"
          d="M11 17h2v-6h-2v6zm1-8.75a1.25 1.25 0 1 0 0-2.5
             1.25 1.25 0 0 0 0 2.5zM12 2a10 10 0 1 0 0 20
             10 10 0 0 0 0-20z"/>
</svg>
""";

	public const string LogoRing = """
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
    <circle cx="12" cy="12" r="8"
            fill="none" stroke="#FFFFFF" stroke-width="4"/>
</svg>
""";

	public const string Lock = """
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
    <path fill="#8992A6"
          d="M12 2a5 5 0 0 0-5 5v3H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h12
             a2 2 0 0 0 2-2v-8a2 2 0 0 0-2-2h-1V7a5 5 0 0 0-5-5zm-3 5
             a3 3 0 0 1 6 0v3H9V7z"/>
</svg>
""";
}
