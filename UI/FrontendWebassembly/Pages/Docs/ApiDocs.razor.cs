// Isolated to this file: WebUtility is not in the module-wide usings, and HtmlEncode is
// the only thing needed from it.
using System.Net;

namespace FrontendWebassembly.Pages.Docs;

public partial class ApiDocs : IAsyncDisposable
{
	private static readonly string[] Languages = ["cURL", "C#"];

	// The preamble sections plus every documented operation, in the order they appear in
	// the document. The observer reports the topmost visible one.
	private static readonly string[] SpiedAnchors =
	[
		"overview",
		"authentication",
		"errors",
		.. ApiDocsContent.Sections.SelectMany(section => section.Endpoints).Select(endpoint => endpoint.Anchor)
	];

	private DotNetObjectReference<ApiDocs>? _selfReference;
	private string? _visibleAnchor;

	[Inject]
	private IJSRuntime JSRuntime { get; set; } = default!;

	/// <summary>
	/// Deep link to one operation, e.g. /docs/api/create-endorsement. Highlights that
	/// entry in the contents list so a shared link lands somewhere obvious.
	/// </summary>
	[Parameter]
	public string? Section { get; set; }

	private string _activeLanguage = Languages[0];
	private string? _copiedSample;
	private bool _isNavOpen;

	// Doubled $ so the JSON braces in the body are literal and only {{...}} interpolates.
	private static string AuthSample =>
		$$"""
		curl -X POST "{{ApiDocsContent.BaseUrl}}{{ApiDocsContent.TokenPath}}" \
		  -H "Content-Type: application/json" \
		  -d '{ "username": "<your username>", "password": "<your password>" }'
		""";

	private string NavClass => _isNavOpen
		? "api-docs-nav is-open"
		: "api-docs-nav";

	private void ToggleNav() => _isNavOpen = !_isNavOpen;

	// The rail overlays the document on small screens, so following a link has to close
	// it. Handled here on the container rather than on each anchor: an @onclick on an
	// in-page "#" link makes Blazor's router treat the fragment as a route, which
	// resolves to nothing and renders the not-found page. Listening on the parent lets
	// the anchors stay plain, so the browser performs the jump itself.
	private void OnNavClicked() => _isNavOpen = false;

	/// <summary>
	/// An in-page link that survives Blazor's routing. index.html sets
	/// <c>&lt;base href="/" /&gt;</c>, and a bare <c>#anchor</c> resolves against that
	/// base rather than the current URL — so it would navigate to <c>/#anchor</c>, the
	/// OnePlatform home page, instead of jumping within this document. Including the
	/// page path keeps the browser on this page.
	/// </summary>
	private const string PagePath = "/docs/api";

	private static string Fragment(string anchor) => $"{PagePath}#{anchor}";

	// Starts once the sections exist in the DOM.
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
		{
			return;
		}

		_selfReference = DotNetObjectReference.Create(this);

		try
		{
			await JSRuntime.InvokeVoidAsync(
				"scrollSpy.start",
				SpiedAnchors,
				_selfReference,
				nameof(OnSectionInView));
		}
		catch (JSException)
		{
			// Without the observer the rail simply never highlights, which is a
			// cosmetic loss - the document itself still reads and navigates.
		}
	}

	/// <summary>
	/// Called from JS when the topmost visible section changes.
	/// </summary>
	[JSInvokable]
	public Task OnSectionInView(string anchor)
	{
		if (string.Equals(_visibleAnchor, anchor, StringComparison.Ordinal))
		{
			return Task.CompletedTask;
		}

		_visibleAnchor = anchor;

		return InvokeAsync(StateHasChanged);
	}

	// Scroll position wins over the route parameter: once the reader moves, the rail
	// should follow them rather than keep pointing at the link they arrived on. The
	// route parameter only seeds the highlight for a deep link, before the observer
	// has reported anything.
	private string NavLinkClass(string anchor)
	{
		var active = _visibleAnchor is not null
			? string.Equals(_visibleAnchor, anchor, StringComparison.OrdinalIgnoreCase)
			: string.Equals(Section, anchor, StringComparison.OrdinalIgnoreCase);

		return active
			? "api-docs-nav-link is-active"
			: "api-docs-nav-link";
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			await JSRuntime.InvokeVoidAsync("scrollSpy.stop");
		}
		catch (JSException)
		{
			// The page is going away; a failed teardown is not worth surfacing.
		}
		catch (JSDisconnectedException)
		{
			// The circuit is already gone.
		}

		_selfReference?.Dispose();
	}

	private string LanguageClass(string language) =>
		_activeLanguage == language
			? "ats-segment-btn api-docs-lang-btn active"
			: "ats-segment-btn api-docs-lang-btn";

	private static string MethodClass(string method) => method switch
	{
		"GET" => "is-get",
		"POST" => "is-post",
		"PATCH" => "is-patch",
		"DELETE" => "is-delete",
		_ => "is-get"
	};

	private string SampleFor(ApiEndpointDoc endpoint) =>
		_activeLanguage == "C#"
			? endpoint.CSharpSample
			: endpoint.CurlSample;

	private bool IsCopied(string? value) =>
		!string.IsNullOrEmpty(value) && string.Equals(_copiedSample, value, StringComparison.Ordinal);

	private string CopyIcon(string sample) =>
		IsCopied(sample)
			? Icons.Material.Outlined.Check
			: Icons.Material.Outlined.ContentCopy;

	private async Task CopyAsync(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}

		try
		{
			await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", value);

			_copiedSample = value;
			StateHasChanged();

			await Task.Delay(1500);

			if (string.Equals(_copiedSample, value, StringComparison.Ordinal))
			{
				_copiedSample = null;
				StateHasChanged();
			}
		}
		catch (JSException)
		{
			// Clipboard access is unavailable in insecure or restricted contexts. The
			// sample is still selectable, so failing quietly beats an error toast.
		}
	}

	/// <summary>
	/// Colours a JSON response sample. Deliberately the same shape as the platform log
	/// viewer's highlighter, reusing its shared .ats-json-* classes rather than pulling
	/// in a syntax-highlighting library for a handful of short samples.
	/// </summary>
	private static MarkupString HighlightJson(string sample)
	{
		var html = WebUtility.HtmlEncode(sample);

		html = Regex.Replace(
			html,
			@"(&quot;[^&]*?&quot;)(\s*:)|(&quot;[^&]*?&quot;)|\b(true|false|null|-?\d+(?:\.\d+)?)\b",
			match =>
			{
				if (match.Groups[2].Success)
				{
					return $"<span class=\"ats-json-key\">{match.Groups[1].Value}</span>{match.Groups[2].Value}";
				}

				if (match.Groups[3].Success)
				{
					return $"<span class=\"ats-json-string\">{match.Groups[3].Value}</span>";
				}

				return $"<span class=\"ats-json-number\">{match.Value}</span>";
			});

		return new MarkupString(html);
	}
}
