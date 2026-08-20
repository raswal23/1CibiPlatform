using System.Text;

namespace BuildingBlocks.Pagination;

// Encodes keyset cursors as base64("field1|field2|...").
// A cursor that fails to decode (malformed base64, wrong field count) returns null
// and the caller silently falls back to the first page — a bad cursor is never an
// error. KISS trade-offs, by design: null and "" fields collapse on round-trip, a
// '|' inside a field value shifts the field count so the cursor decodes as null,
// and a stale cursor whose field values no longer parse degrades the same way.
public static class CursorCodec
{
	public static string Encode(params string?[] fields) =>
		Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|', fields)));

	// null on null/whitespace/malformed base64 or wrong field count. Never throws.
	public static string[]? Decode(string? cursor, int fieldCount)
	{
		if (string.IsNullOrWhiteSpace(cursor))
			return null;
		try
		{
			var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
			return parts.Length == fieldCount ? parts : null;
		}
		catch (FormatException)
		{
			return null;
		}
	}
}
