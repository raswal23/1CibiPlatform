namespace FrontendWebassembly.ShareData.EmploymentVerification;

/// <summary>
/// The Employment Verification console routes, in one place because three separate
/// things have to agree on each string and nothing enforces that at compile time:
/// the page's own <c>@page</c> directive, the navbar tab in <c>EVLayout</c>, and the
/// legacy redirect.
/// <para>
/// <see cref="Landing"/> is the route registered as submenu 9 in
/// <c>ShareData/Auth/SubMenuList.cs</c>, which is what the home application card is
/// built from. It stays a redirect rather than becoming a real page so the card, any
/// bookmarks and the backend permission seed data keep working unchanged.
/// </para>
/// </summary>
public static class EmploymentVerificationRoutes
{
	public const string Landing = "/employmentverification/verification";
	public const string NeedsRequest = "/employmentverification/requests";
	public const string Tracking = "/employmentverification/tracking";
	public const string Contacts = "/employmentverification/contacts";
}
