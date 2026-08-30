namespace FrontendWebassembly.Pages.Docs;

/// <summary>
/// One documented field on a request or response body.
/// </summary>
public record ApiFieldDoc(string Name, string Type, bool Required, string Description);

/// <summary>
/// One documented operation. Samples are held as literal strings rather than generated
/// from the DTOs so the page shows exactly what a caller should send, including the
/// header and the shell quoting.
/// </summary>
public record ApiEndpointDoc
{
	public string Anchor { get; init; } = string.Empty;

	public string Title { get; init; } = string.Empty;

	public string Method { get; init; } = string.Empty;

	public string Path { get; init; } = string.Empty;

	public string Summary { get; init; } = string.Empty;

	public IReadOnlyList<ApiFieldDoc> Fields { get; init; } = [];

	public string CurlSample { get; init; } = string.Empty;

	public string CSharpSample { get; init; } = string.Empty;

	public string ResponseStatus { get; init; } = string.Empty;

	public string ResponseSample { get; init; } = string.Empty;

	public IReadOnlyList<string> Notes { get; init; } = [];
}

public record ApiSectionDoc(string Title, IReadOnlyList<ApiEndpointDoc> Endpoints);

/// <summary>
/// The documentation itself, as a typed model rather than markdown files. Compiler-checked
/// and refactor-safe, and it avoids an nginx trap: `.md` has no location block, so a
/// mistyped content path would return index.html with HTTP 200 instead of a 404.
/// </summary>
public static class ApiDocsContent
{
	public const string BaseUrl = "https://oneplatform.cibi.com.ph/";

	public const string TokenPath = "/token/generatetoken";

	public static IReadOnlyList<ApiSectionDoc> Sections { get; } =
	[
		new ApiSectionDoc("Endorsements",
		[
			new ApiEndpointDoc
			{
				Anchor = "create-endorsement",
				Title = "Create an endorsement",
				Method = "POST",
				Path = "/publicapi/ats/endorsements",
				Summary =
					"Creates a single background-check order and emails the application "
					+ "form to the subject. The order is attributed to the client your "
					+ "access token belongs to.",
				Fields =
				[
					new ApiFieldDoc("firstName", "string", true, "Subject's first name. Max 50 characters."),
					new ApiFieldDoc("lastName", "string", true, "Subject's last name. Max 50 characters."),
					new ApiFieldDoc("middleInitial", "string", false, "Optional. Leave blank or omit when the subject has no middle name."),
					new ApiFieldDoc("emailAddress", "string", true, "Where the application form is sent."),
					new ApiFieldDoc("mobileNumber", "string", true, "Exactly 11 digits, e.g. 09171234567."),
					new ApiFieldDoc("package", "string", true, "A package name from GET /publicapi/ats/packages."),
					new ApiFieldDoc("orderType", "string", true, "Normal or Rush.")
				],
				CurlSample =
					"""
					curl -X POST "https://api.cibi.com.ph/publicapi/ats/endorsements" \
					  -H "Authorization: Bearer $TOKEN" \
					  -H "Content-Type: application/json" \
					  -d '{
					        "firstName": "Juan",
					        "lastName": "Dela Cruz",
					        "middleInitial": "",
					        "emailAddress": "juan.delacruz@example.com",
					        "mobileNumber": "09171234567",
					        "package": "CRIMINAL RECORDS CHECK",
					        "orderType": "Normal"
					      }'
					""",
				CSharpSample =
					"""
					using var client = new HttpClient();
					client.DefaultRequestHeaders.Authorization =
					    new AuthenticationHeaderValue("Bearer", token);

					var response = await client.PostAsJsonAsync(
					    "https://api.cibi.com.ph/publicapi/ats/endorsements",
					    new
					    {
					        firstName = "Juan",
					        lastName = "Dela Cruz",
					        middleInitial = "",
					        emailAddress = "juan.delacruz@example.com",
					        mobileNumber = "09171234567",
					        package = "CRIMINAL RECORDS CHECK",
					        orderType = "Normal"
					    });

					response.EnsureSuccessStatusCode();
					""",
				ResponseStatus = "200 OK",
				ResponseSample = "true"
			},

			new ApiEndpointDoc
			{
				Anchor = "create-bulk-endorsement",
				Title = "Create endorsements from a CSV",
				Method = "POST",
				Path = "/publicapi/ats/endorsements/bulk",
				Summary =
					"Uploads a CSV of subjects and queues it for processing. The response "
					+ "returns immediately with a file id; the rows are parsed within "
					+ "seconds. Poll the file id to see the result.",
				Fields =
				[
					new ApiFieldDoc("file", "file", true, "The CSV. Max 10 MB."),
					new ApiFieldDoc("package", "string", true, "Applied to every row in the file."),
					new ApiFieldDoc("orderType", "string", true, "Normal or Rush. Applied to every row.")
				],
				CurlSample =
					"""
					curl -X POST "https://api.cibi.com.ph/publicapi/ats/endorsements/bulk" \
					  -H "Authorization: Bearer $TOKEN" \
					  -F "file=@subjects.csv" \
					  -F "package=CRIMINAL RECORDS CHECK" \
					  -F "orderType=Normal"
					""",
				CSharpSample =
					"""
					using var content = new MultipartFormDataContent
					{
					    { new StreamContent(File.OpenRead("subjects.csv")), "file", "subjects.csv" },
					    { new StringContent("CRIMINAL RECORDS CHECK"), "package" },
					    { new StringContent("Normal"), "orderType" }
					};

					var response = await client.PostAsync(
					    "https://api.cibi.com.ph/publicapi/ats/endorsements/bulk",
					    content);
					""",
				ResponseStatus = "202 Accepted",
				ResponseSample =
					"""
					{
					  "fileId": "0199a1c4-3b7e-7c21-9f3a-2b8c6d5e4f10",
					  "accepted": true
					}
					""",
				Notes =
				[
					"Required columns: LastName, FirstName, MiddleInitial, EmailAddress, MobileNumber.",
					"MiddleInitial may be left blank — many subjects have no middle name.",
					"A row that fails validation is skipped, not fatal. The rest of the file still creates orders, and the skipped rows are listed with a reason by the endpoint below."
				]
			},

			new ApiEndpointDoc
			{
				Anchor = "bulk-status",
				Title = "Get a bulk upload's result",
				Method = "GET",
				Path = "/publicapi/ats/endorsements/bulk/{fileId}",
				Summary =
					"Returns how the CSV was parsed: how many rows became orders, and "
					+ "which rows were skipped with the reason for each.",
				CurlSample =
					"""
					curl "https://api.cibi.com.ph/publicapi/ats/endorsements/bulk/$FILE_ID" \
					  -H "Authorization: Bearer $TOKEN"
					""",
				CSharpSample =
					"""
					var upload = await client.GetFromJsonAsync<BulkUploadStatus>(
					    $"https://api.cibi.com.ph/publicapi/ats/endorsements/bulk/{fileId}");
					""",
				ResponseStatus = "200 OK",
				ResponseSample =
					"""
					{
					  "upload": {
					    "fileId": "0199a1c4-3b7e-7c21-9f3a-2b8c6d5e4f10",
					    "fileName": "subjects.csv",
					    "status": "Done",
					    "acceptedRowCount": 24,
					    "rejectedRowCount": 1,
					    "rejectedRows": [
					      { "rowNumber": 7, "reason": "Mobile number must be 11 digits." }
					    ]
					  }
					}
					""",
				Notes =
				[
					"status is Pending until the file is picked up, then Processing, then Done.",
					"rejectedRows is empty until parsing completes."
				]
			}
		]),

		new ApiSectionDoc("Orders",
		[
			new ApiEndpointDoc
			{
				Anchor = "list-orders",
				Title = "List orders",
				Method = "GET",
				Path = "/publicapi/ats/orders",
				Summary =
					"Returns your client's orders, newest first, using cursor pagination. "
					+ "Pass the returned cursor to fetch the next page.",
				Fields =
				[
					new ApiFieldDoc("cursor", "string", false, "Cursor from the previous page. Omit for the first page."),
					new ApiFieldDoc("pageSize", "integer", false, "1–100. Defaults to 10."),
					new ApiFieldDoc("searchTerm", "string", false, "Matches subject name, email or package."),
					new ApiFieldDoc("startDate", "date", false, "Only orders created on or after this date."),
					new ApiFieldDoc("endDate", "date", false, "Only orders created on or before this date.")
				],
				CurlSample =
					"""
					curl "https://api.cibi.com.ph/publicapi/ats/orders?pageSize=25" \
					  -H "Authorization: Bearer $TOKEN"
					""",
				CSharpSample =
					"""
					var page = await client.GetFromJsonAsync<OrderPage>(
					    "https://api.cibi.com.ph/publicapi/ats/orders?pageSize=25");
					""",
				ResponseStatus = "200 OK",
				ResponseSample =
					"""
					{
					  "orders": {
					    "items": [
					      {
					        "emailInvitationRequestId": "0199a1c4-...",
					        "subjectName": "Juan Dela Cruz",
					        "orderStatus": "In Progress",
					        "selectedPackage": "CRIMINAL RECORDS CHECK"
					      }
					    ],
					    "nextCursor": "eyJ2IjoiMjAyNi0wOC0zMCJ9",
					    "totalCount": 128
					  }
					}
					"""
			},

			new ApiEndpointDoc
			{
				Anchor = "get-order",
				Title = "Get an order",
				Method = "GET",
				Path = "/publicapi/ats/orders/{orderId}",
				Summary =
					"Returns one order's current status, its ticket number once raised, "
					+ "and the events that got it there.",
				CurlSample =
					"""
					curl "https://api.cibi.com.ph/publicapi/ats/orders/$ORDER_ID" \
					  -H "Authorization: Bearer $TOKEN"
					""",
				CSharpSample =
					"""
					var order = await client.GetFromJsonAsync<OrderDetail>(
					    $"https://api.cibi.com.ph/publicapi/ats/orders/{orderId}");
					""",
				ResponseStatus = "200 OK",
				ResponseSample =
					"""
					{
					  "order": {
					    "orderId": "0199a1c4-...",
					    "firstName": "Juan",
					    "lastName": "Dela Cruz",
					    "package": "CRIMINAL RECORDS CHECK",
					    "orderStatus": "In Progress",
					    "applicationFormStatus": "Done",
					    "ticketNumber": "202608260001",
					    "history": [
					      { "eventType": "OrderCreated", "newStatus": "Pending Candidate Info", "source": "PublicApi" }
					    ]
					  }
					}
					""",
				Notes =
				[
					"Returns 404 when the order does not belong to your client — the same response as an order that does not exist."
				]
			},

			new ApiEndpointDoc
			{
				Anchor = "withdraw-order",
				Title = "Withdraw an order",
				Method = "PATCH",
				Path = "/publicapi/ats/orders/{orderId}/withdraw",
				Summary = "Withdraws an order you own, stopping any further processing.",
				CurlSample =
					"""
					curl -X PATCH \
					  "https://api.cibi.com.ph/publicapi/ats/orders/$ORDER_ID/withdraw" \
					  -H "Authorization: Bearer $TOKEN"
					""",
				CSharpSample =
					"""
					var response = await client.PatchAsync(
					    $"https://api.cibi.com.ph/publicapi/ats/orders/{orderId}/withdraw",
					    content: null);
					""",
				ResponseStatus = "200 OK",
				ResponseSample = "true",
				Notes =
				[
					"Returns 409 when the order is already withdrawn or completed."
				]
			},

			new ApiEndpointDoc
			{
				Anchor = "download-report",
				Title = "Download an order's documents",
				Method = "POST",
				Path = "/publicapi/ats/orders/{orderId}/report",
				Summary = "Returns the requested documents for a completed order as a ZIP archive.",
				Fields =
				[
					new ApiFieldDoc("documentTypes", "string[]", true, "Which documents to include in the archive.")
				],
				CurlSample =
					"""
					curl -X POST \
					  "https://api.cibi.com.ph/publicapi/ats/orders/$ORDER_ID/report" \
					  -H "Authorization: Bearer $TOKEN" \
					  -H "Content-Type: application/json" \
					  -d '{ "documentTypes": ["Report"] }' \
					  --output report.zip
					""",
				CSharpSample =
					"""
					var response = await client.PostAsJsonAsync(
					    $"https://api.cibi.com.ph/publicapi/ats/orders/{orderId}/report",
					    new { documentTypes = new[] { "Report" } });

					await using var file = File.Create("report.zip");
					await response.Content.CopyToAsync(file);
					""",
				ResponseStatus = "200 OK",
				ResponseSample = "(binary — application/zip)"
			}
		]),

		new ApiSectionDoc("Reference",
		[
			new ApiEndpointDoc
			{
				Anchor = "list-packages",
				Title = "List available packages",
				Method = "GET",
				Path = "/publicapi/ats/packages",
				Summary =
					"Returns the packages your client is entitled to. Use a name from "
					+ "this list as the package field when creating an endorsement.",
				Fields =
				[
					new ApiFieldDoc("cursor", "string", false, "Cursor from the previous page."),
					new ApiFieldDoc("pageSize", "integer", false, "1–100. Defaults to 10."),
					new ApiFieldDoc("searchTerm", "string", false, "Matches package name or description.")
				],
				CurlSample =
					"""
					curl "https://api.cibi.com.ph/publicapi/ats/packages" \
					  -H "Authorization: Bearer $TOKEN"
					""",
				CSharpSample =
					"""
					var packages = await client.GetFromJsonAsync<PackagePage>(
					    "https://api.cibi.com.ph/publicapi/ats/packages");
					""",
				ResponseStatus = "200 OK",
				ResponseSample =
					"""
					{
					  "packages": {
					    "items": [
					      { "packageId": 12, "packageName": "CRIMINAL RECORDS CHECK", "isActive": true }
					    ],
					    "nextCursor": null,
					    "totalCount": 4
					  }
					}
					"""
			}
		])
	];

	/// <summary>
	/// The status codes every endpoint can return, documented once instead of repeated
	/// on each operation.
	/// </summary>
	public static IReadOnlyList<(string Code, string Meaning, string Tone)> StatusCodes { get; } =
	[
		("200", "The request succeeded.", "success"),
		("202", "Accepted for processing. Used by the bulk upload, which is parsed in the background.", "success"),
		("400", "The request body failed validation. The response says which field and why.", "warn"),
		("401", "The access token is missing, expired or invalid.", "warn"),
		("404", "The record does not exist, or does not belong to your client.", "warn"),
		("409", "The record exists but is in a state that does not allow this operation.", "warn"),
		("429", "Too many requests. Slow down and retry.", "warn"),
		("500", "Something failed on our side. Safe to retry.", "error")
	];
}
