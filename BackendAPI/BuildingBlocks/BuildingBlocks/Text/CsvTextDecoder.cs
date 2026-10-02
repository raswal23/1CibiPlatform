using System.Text;

namespace BuildingBlocks.Text;

/// <summary>
/// Decodes user-uploaded CSV bytes to text. Excel's "CSV (Comma delimited)" export on
/// Windows writes Windows-1252/ANSI with no BOM; decoding it as UTF-8 turns Ñ (0xD1)
/// and ñ (0xF1) into U+FFFD. Strategy: honor a BOM if present, else strict UTF-8,
/// else Windows-1252 (Latin1 if code pages are unavailable).
/// </summary>
/// <remarks>
/// Mirrored in UI\FrontendWebassembly\SharedService\CsvTextDecoder.cs (the WASM app
/// shares no project with the backend) - keep the two copies in sync.
/// </remarks>
public static class CsvTextDecoder
{
	private static readonly UTF8Encoding StrictUtf8 =
		new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	static CsvTextDecoder()
	{
		// Idempotent: repeated registrations of the same provider are ignored.
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	public static async Task<string> DecodeAsync(Stream stream, CancellationToken ct = default)
	{
		// Sources (browser file, OSS download, IFormFile) are non-seekable, and the
		// fallback strategy needs the bytes twice - buffer once. Uploads are capped
		// at 25 MB upstream.
		using var buffer = new MemoryStream();
		await stream.CopyToAsync(buffer, ct);
		return Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
	}

	public static string Decode(ReadOnlySpan<byte> bytes)
	{
		// A BOM is an explicit declaration - honor it and strip it. Encoding.GetString
		// does NOT strip BOMs the way StreamReader does; leaving U+FEFF on the first
		// header cell breaks header matching.
		if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			return Encoding.UTF8.GetString(bytes[3..]);
		if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			return Encoding.Unicode.GetString(bytes[2..]);
		if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			return Encoding.BigEndianUnicode.GetString(bytes[2..]);

		try
		{
			return StrictUtf8.GetString(bytes);
		}
		catch (DecoderFallbackException)
		{
			return AnsiFallback.GetString(bytes);
		}
	}

	private static Encoding AnsiFallback
	{
		get
		{
			try
			{
				return Encoding.GetEncoding(1252); // Excel ANSI on Western Windows
			}
			catch (NotSupportedException)
			{
				return Encoding.Latin1; // identical to 1252 for Ñ/ñ and 0xA0-0xFF
			}
		}
	}
}
