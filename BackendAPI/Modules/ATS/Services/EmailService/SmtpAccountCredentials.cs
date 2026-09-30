namespace ATS.Services.EmailService;

/// <summary>
/// Everything one <see cref="SmtpConnectionPool"/> needs to open a session, as an immutable
/// snapshot taken at the moment the pool was built.
/// </summary>
/// <remarks>
/// The pool used to read these from <c>IConfiguration</c> in its constructor, which silently
/// made it a one-account object: there was exactly one <c>Email:ATSGmail</c> section, so there
/// could only ever be one pool. Passing the credentials in is what lets a pool exist per
/// registered account.
///
/// A snapshot rather than a live reference to the account row, because a pool holds
/// authenticated sessions opened with THESE credentials. If the row changed underneath it, the
/// open sessions would still be using the old password while the object claimed the new one -
/// so a credential change disposes the pool and builds a new one instead of mutating this.
///
/// <paramref name="AppPassword"/> is the plaintext, already unprotected. It lives only in
/// memory and must never be logged or put in a DTO.
/// </remarks>
public sealed record SmtpAccountCredentials(
	int AtsEmailAccountId,
	string DisplayName,
	string EmailAddress,
	string AppPassword,
	string SmtpHost,
	int SmtpPort);
