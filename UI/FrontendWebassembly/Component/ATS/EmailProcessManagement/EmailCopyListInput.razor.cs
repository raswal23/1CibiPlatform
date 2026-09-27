using FrontendWebassembly.SharedService;

namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// Types mailboxes into the one comma-separated string <c>EmailProcessDetails.CCEmail</c> stores.
/// </summary>
/// <remarks>
/// Binds with <c>@bind-Value</c> and emits the NORMALISED list, so the dialog never handles the
/// delimiter and never has to trim what comes back.
///
/// Checks each address as it is added rather than on save, because a malformed fragment is not a
/// cosmetic problem: the send path hands every fragment to <c>MimeKit.MailboxAddress.Parse</c>,
/// which throws for the WHOLE notice rather than for the one bad entry. The server enforces the
/// same rules again - this is the cheap early report, not the authority.
///
/// Shape checking is delegated to <see cref="EmailValidationService"/> rather than restated, so
/// the UI keeps one definition of an email address. Everything list-shaped - the delimiter, the
/// duplicate rule, the two length caps - lives in <see cref="EmailCopyListDraft"/>.
/// </remarks>
public partial class EmailCopyListInput
{
	[Inject] private EmailValidationService EmailValidation { get; set; } = default!;

	/// <summary>The copy list as a comma-separated string.</summary>
	[Parameter] public string Value { get; set; } = string.Empty;

	[Parameter] public EventCallback<string> ValueChanged { get; set; }

	[Parameter] public string InputId { get; set; } = string.Empty;

	[Parameter] public string AriaLabel { get; set; } = "Email addresses to copy";

	[Parameter] public string Placeholder { get; set; } = "Type an address and press Enter";

	[Parameter] public string Hint { get; set; } = "Press Enter or a comma to add. Backspace removes the last one.";

	[Parameter] public bool Disabled { get; set; }

	private readonly List<string> _addresses = [];

	private string _draft = string.Empty;
	private string? _error;

	// The last value this component emitted. Without it, OnParametersSet would re-split the
	// parent's echo of our own emission on every render and throw away a draft the operator is
	// halfway through typing.
	private string _lastEmitted = string.Empty;

	public bool HasError => _error is not null;

	public int AddressCount => _addresses.Count;

	private string _describedBy => HasError
		? $"{InputId}-hint {InputId}-error"
		: $"{InputId}-hint";

	protected override void OnParametersSet()
	{
		if (string.Equals(Value, _lastEmitted, StringComparison.Ordinal))
		{
			return;
		}

		_lastEmitted = Value;

		_addresses.Clear();
		_addresses.AddRange(EmailCopyListDraft.Split(Value));
	}

	private async Task OnInput(ChangeEventArgs args)
	{
		_draft = args.Value?.ToString() ?? string.Empty;

		// A comma is the delimiter, so a pasted list lands as several chips at once rather than
		// waiting for Enter. Typed commas take the same path, which is what makes the field read
		// as a list rather than as a line of text.
		if (_draft.Contains(EmailCopyListDraft.Delimiter))
		{
			await CommitDraft();
			return;
		}

		if (_error is not null)
		{
			// Retyping clears a stale complaint immediately; leaving it up while the operator
			// fixes the address reads as though the fix did not register.
			_error = null;
		}
	}

	private async Task OnKeyDown(KeyboardEventArgs args)
	{
		if (args.Key == "Enter")
		{
			await CommitDraft();
			return;
		}

		// Backspace on an empty field removes the last chip, which is how every chip input the
		// operator has used before behaves - and the alternative is reaching for a mouse to undo
		// a mis-press.
		if (args.Key == "Backspace" && _draft.Length == 0 && _addresses.Count > 0)
		{
			await RemoveAt(_addresses.Count - 1);
		}
	}

	/// <summary>
	/// Moves everything typed into chips. Runs on Enter, on a comma, and on blur - the last one so
	/// an address typed without a keystroke to commit it is not silently dropped when the operator
	/// clicks Save.
	/// </summary>
	private async Task CommitDraft()
	{
		var candidates = EmailCopyListDraft.Split(_draft);

		if (candidates.Count == 0)
		{
			_draft = string.Empty;
			_error = null;

			return;
		}

		var accepted = new List<string>();
		_error = null;

		foreach (var candidate in candidates)
		{
			var rejection = CheckAddress(candidate);

			if (rejection is not null)
			{
				_error = rejection;
				break;
			}

			accepted.Add(candidate);
		}

		if (accepted.Count > 0)
		{
			_addresses.AddRange(accepted);

			// Keeps only what was rejected, so a pasted list of five whose fourth was malformed
			// does not lose the fifth - it stays in the box to be fixed.
			_draft = accepted.Count == candidates.Count
				? string.Empty
				: string.Join(EmailCopyListDraft.Delimiter, candidates.Skip(accepted.Count));

			await EmitAsync();
		}
	}

	/// <summary>The reason an address cannot go into the list, or null when it can.</summary>
	private string? CheckAddress(string candidate)
	{
		// Checked before the shape rule, and the value is deliberately not echoed: a 900-character
		// paste would otherwise be repeated into the dialog.
		if (candidate.Length > EmailCopyListDraft.MaxAddressLength)
		{
			return $"An email address cannot exceed {EmailCopyListDraft.MaxAddressLength} characters.";
		}

		var shapeError = EmailValidation.ValidateEmail(candidate);

		if (shapeError is not null)
		{
			return $"'{candidate}' is not a valid email address.";
		}

		if (EmailCopyListDraft.Contains(_addresses, candidate))
		{
			return $"'{candidate}' is already in the copy list.";
		}

		var projectedLength = EmailCopyListDraft.NormalizedLength([.. _addresses, candidate]);

		if (projectedLength > EmailCopyListDraft.MaxLength)
		{
			return $"The copy list cannot exceed {EmailCopyListDraft.MaxLength} characters.";
		}

		return null;
	}

	/// <summary>
	/// Drops the chip at <paramref name="index"/> and re-emits the list.
	/// </summary>
	/// <remarks>
	/// The bounds guard is for a click that arrives after the list already changed - a real case,
	/// since blur commits a typed address before the click on a remove button lands. It is not a
	/// substitute for the caller passing the right index, and it cannot report being wrong: this
	/// runs in an event handler, so returning silently is the only option. That is exactly what hid
	/// a bug here once, when the markup closed over a shared <c>for</c> variable and every button
	/// asked for one-past-the-end. See the capture comment in <c>EmailCopyListInput.razor</c>.
	/// </remarks>
	private async Task RemoveAt(int index)
	{
		if (index < 0 || index >= _addresses.Count)
		{
			return;
		}

		_addresses.RemoveAt(index);
		_error = null;

		await EmitAsync();
	}

	private async Task EmitAsync()
	{
		// Already trimmed and de-duplicated by CheckAddress, so this is the canonical form the
		// server stores - joining is all that is left to do.
		_lastEmitted = string.Join(EmailCopyListDraft.Delimiter, _addresses);

		await ValueChanged.InvokeAsync(_lastEmitted);
	}
}
