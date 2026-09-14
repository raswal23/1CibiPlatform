namespace ATS.Services.AuditTrail;

/// <summary>
/// Serializes a command to JSON with sensitive values masked, so the audit trail can answer
/// "what did they change" without copying government IDs and birthdates into a second
/// table that lives for 30 days.
/// Pure and static so the masking rules can be tested without a database or a request.
/// </summary>
public static class AtsAuditRedactor
{
	public const string Mask = "***";

	// The marker stored instead of a payload that would be too large to keep. The entry is
	// still worth writing - who did what is the point, the body is supporting detail.
	public const string OversizedPayload = """{"_note":"Payload omitted: too large."}""";

	public const string UnserializablePayload = """{"_note":"Payload omitted: not serializable."}""";

	// A bulk upload command carries every row of a spreadsheet. Without a cap one action
	// could write a megabyte row, so an oversized payload is dropped rather than stored.
	public const int MaxPayloadCharacters = 8_000;

	/// <summary>
	/// Property names whose values are replaced with <see cref="Mask"/>, matched
	/// case-insensitively and by whole name.
	/// A new command carrying a sensitive field must add its property name here - nothing
	/// else in the pipeline will notice that it leaked.
	/// </summary>
	private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"SSS",
		"SSSIDNumber",
		"TIN",
		"DOB",
		"DateOfBirth",
		"BirthDate",
		"HashToken",
		"Password",
		"NewPassword",
		"CurrentPassword",
		// Matched by WHOLE name, so "Password" above does not cover these. An SMTP app
		// password reaching the audit trail would be readable by anyone with audit access
		// and would outlive the row it came from.
		"AppPassword",
		"SmtpPassword",
		"EncryptedPassword",
		"Token",
		"AccessToken",
		"RefreshToken",
		"Signature",
		"SignatureData",
		"FileContent",
		"FileBytes",
		"Base64",
		"Base64Content"
	};

	// Fully qualified: Quartz publishes its own JsonSerializerOptions, and both namespaces
	// are global usings in this module.
	private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
	{
		// The audit payload is read by a human in a detail panel, so cycles and unmapped
		// types must degrade to a note rather than throwing into the request path.
		ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
		MaxDepth = 32
	};

	/// <summary>
	/// True when a property of this name must never have its value recorded.
	/// </summary>
	/// <remarks>
	/// Exposed so the entity-change interceptor masks the same names this class masks in command
	/// payloads. Two lists would drift, and the one that drifted would leak silently - the
	/// interceptor writes column values straight from the change tracker, where an SMTP password
	/// is as readable as a display name.
	/// </remarks>
	public static bool IsSensitiveProperty(string propertyName) =>
		SensitivePropertyNames.Contains(propertyName);

	public static string Redact<TRequest>(TRequest request)
	{
		try
		{
			// Serialized as the declared type: commands are records, and serializing them
			// as object would lose the derived properties.
			var json = JsonSerializer.Serialize(request, SerializerOptions);

			using var document = JsonDocument.Parse(json);

			var buffer = new ArrayBufferWriter<byte>();

			using (var writer = new Utf8JsonWriter(buffer))
			{
				WriteRedacted(document.RootElement, writer);
			}

			var redacted = Encoding.UTF8.GetString(buffer.WrittenSpan);

			return redacted.Length > MaxPayloadCharacters
				? OversizedPayload
				: redacted;
		}
		catch (Exception exception) when (exception is JsonException or NotSupportedException)
		{
			// A payload that cannot be serialized must not stop the entry being recorded.
			return UnserializablePayload;
		}
	}

	// Walks the whole document rather than only the top level: the sensitive fields live
	// inside nested DTOs and inside arrays of them (AddUserCommand takes a collection).
	private static void WriteRedacted(JsonElement element, Utf8JsonWriter writer)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				writer.WriteStartObject();

				foreach (var property in element.EnumerateObject())
				{
					writer.WritePropertyName(property.Name);

					// Masked whatever the shape of the value: a sensitive name holding an
					// object or an array must not have its contents written out either.
					if (SensitivePropertyNames.Contains(property.Name))
					{
						writer.WriteStringValue(Mask);
					}
					else
					{
						WriteRedacted(property.Value, writer);
					}
				}

				writer.WriteEndObject();
				break;

			case JsonValueKind.Array:
				writer.WriteStartArray();

				foreach (var item in element.EnumerateArray())
				{
					WriteRedacted(item, writer);
				}

				writer.WriteEndArray();
				break;

			default:
				element.WriteTo(writer);
				break;
		}
	}
}
