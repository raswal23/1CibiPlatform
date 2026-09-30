namespace BuildingBlocks.SharedServices.Interfaces;

/// <summary>
/// Two-way protection for a secret that has to be REPLAYED rather than compared.
///
/// This is deliberately not <see cref="IHashService"/>. A password we only ever check can be
/// hashed one way; an SMTP app password must be handed back to the provider verbatim at send
/// time, so it has to be recoverable. Keeping the two behind different interfaces stops the
/// next person reaching for the wrong one - a hashed SMTP password fails at 3am inside a
/// background job, not at the call site.
/// </summary>
public interface ISecretProtector
{
	/// <summary>
	/// Encrypts <paramref name="plaintext"/>, binding it to <paramref name="context"/>.
	///
	/// The context is authenticated but not secret: it is mixed into the tag, so a ciphertext
	/// lifted from one row and pasted onto another fails to decrypt instead of quietly
	/// revealing the first row's secret. Pass something that identifies the exact field and
	/// row, e.g. "ats.AtsEmailAccount.SmtpPassword:{id}".
	/// </summary>
	string Protect(string plaintext, string context);

	/// <summary>
	/// Reverses <see cref="Protect"/>. Throws when the key, the context or the ciphertext
	/// does not match - there is no "best effort" reading of a tampered value.
	/// </summary>
	string Unprotect(string protectedValue, string context);
}
