# ATS AI Assistant — Voice Input — Code Explanation

Companion to [`ats-ai-assistant-voice-input.md`](ats-ai-assistant-voice-input.md), which explains
*what* dictation does and *why* it stops short of auto-sending. This one walks the real call chain
so a developer can change it without opening four files cold. Read §0 first, then use the
"Change X, also check Y" table at the end as the mid-edit lookup.

The feature is small: one ES module (155 lines), one block of a code-behind partial, one block of
markup, one block of scoped CSS. No backend, no endpoint, no gateway route, no database change.

> Every claim below was verified against the code on branch `feature/Update-ReadMe-File`. Where the
> design doc and the code disagree, this document follows the code.

---

## 0. Where the design doc no longer matches the code

| # | `ats-ai-assistant-voice-input.md` says | The code actually does |
|---|---|---|
| **C1** | Key files are `Component/ATS/AIAssistantComponent.razor`, `.razor.cs`, `.razor.css` | One folder deeper: **`Component/ATS/AIAssistant/AIAssistantComponent.*`**. Three of the doc's four table rows are wrong; only the `.js` path is right |
| **C2** | "`voiceDictation.js` — start / stop / destroy" | **Four** exports (`isSupported`, `start`, `stop`, `destroy`), and `start(componentReference, language)` takes **two** arguments. C# always passes `null` for the second, so the recognition locale is whatever `navigator.language` happens to be (§2.3, S6) |
| **C3** | "`Test/Test/Test.csproj` covers BackendAPI only" | It also references `YarpApiGateway.csproj` (line 50) and **`FrontendWebassembly.csproj`** (line 59), and `Test/Test/UI/` already unit-tests two FrontendWebassembly helpers. The substantive point stands — no bUnit, nothing covers dictation (§9) |
| **C4** | Hiding the button is the whole story for unsupported browsers; the code comment says a missing button "is otherwise invisible" | `LogDictationFailureAsync` runs **only inside the `catch`**. When the import succeeds and `isSupported()` returns `false`, nothing is logged. Unsupported browser, insecure origin and broken module are all equally silent (§5.1) |
| **C5** | The pulse is "neutralized" by the reduced-motion block, listed as handled | True — and the consequence is worth stating: `animation-duration: 0.01ms` plus `iteration-count: 1` removes the **only** motion cue, leaving the solid fill and the `aria-live` text (§7.1) |
| **C6** | *(says nothing about theming)* | `@keyframes ats-assistant-mic-pulse` hardcodes `rgba(29, 95, 209, .45)` — exactly `#1d5fd1`, the light-mode end stop of `--c-primary-gradient`. That breaks the "colour literals live only in `theme.css`" rule and the pulse keeps glowing light-mode blue in dark mode (§8.1) |
| **C7** | "the last one to start wins rather than both receiving results" | Understated. `stop()` ends with `detach(state)`, so the *losing* component never receives `OnSpeechEndedAsync`: its button keeps pulsing over a dead mic, and its next click calls JS `stop()` — killing the **winner's** live session (§4.2) |
| **C8** | Manual test 9: "Narrow viewport (≤600px)" | The guide requires 390px and the 601–960px band. The `@media (max-width: 600px)` block contains **no composer rules at all**, so that test exercises flex sizing, not the query it appears to target (§8.2) |

The doc's *behavioural* claims all check out and can be trusted: the
`_committedMessage`/`_currentMessage` split, typing re-baselining, the Chrome `onend` restart,
`SendAsync`/`Clear` stopping the mic first, the error-code-to-wording table, `IAsyncDisposable`
calling `destroy()`, the `aria-pressed`/`aria-live` wiring, and the absence of any `index.html`,
`.csproj` or `FrontendServiceConfig.cs` change. One claim could not be checked — "it was
`IDisposable`" — because `git` is unavailable here; `@implements IAsyncDisposable` is today's truth.

---

## 1. Scope

| Concern | File | Lines |
|---|---|---|
| Recognizer lifecycle | `UI/FrontendWebassembly/wwwroot/js/ats/voiceDictation.js` | whole file |
| Interop, state, callbacks | `.../Component/ATS/AIAssistant/AIAssistantComponent.razor.cs` | 22, 41–49, 86–283, 460–463, 566–593 |
| Button, status region, hint | `.../AIAssistantComponent.razor` | 295–320, 341–349 |
| Mic styling and pulse | `.../AIAssistantComponent.razor.css` | 717–798 (the `/* Dictation */` section) |

Confirmed by inspection: `wwwroot/index.html:141-156` lists every `<script>` the app loads and
`voiceDictation.js` is **not** among them, while `js/ats/atsAssistant#[.{fingerprint}].js` (line 148)
is. That is intended — the module is `import()`-ed on demand, like `js/generic/safeSignaturePad.js`,
which is also absent. No `ServiceConfig` file mentions dictation. Because it bypasses the
`index.html` placeholder pass, the path is a literal filename and must **not** gain a
`#[.{fingerprint}]` marker.

---

## 2. The full trace — one dictated sentence

### 2.1 First render: import, then probe support

`OnAfterRenderAsync(firstRender)` (`.razor.cs:73`) calls `InitializeDictationAsync()` once —
`OnAfterRender`, not `OnInitialized`, because JS interop is unavailable during initialization in
WebAssembly. Lines 86–110, abridged:

```csharp
		// No IsPageAuthorized guard here: SecurePageBase sets that flag after an await, so
		// the first render happens while it is still false. …
		try
		{
			_dictationModule = await JS.InvokeAsync<IJSObjectReference>("import", DictationModulePath);

			_isSpeechSupported = await _dictationModule.InvokeAsync<bool>("isSupported");
		}
		catch (Exception exception)
		{
			// Dictation is an enhancement. … It is logged rather than swallowed: a missing
			// button is otherwise invisible.
			_isSpeechSupported = false;

			await LogDictationFailureAsync(exception.Message);
		}
```

The missing `IsPageAuthorized` guard is deliberate — every other ATS component has one, and the
comment explains why this one cannot. The `catch` is the **only** logging path (**C4**).
`DictationModulePath` is line 22, `"./js/ats/voiceDictation.js"`.

### 2.2 The click

`AIAssistantComponent.razor:305-313`, verbatim:

```razor
                @if (_isSpeechSupported)
                {
                    <button type="button"
                            class="@GetMicClass()"
                            aria-label="@(_isListening ? "Stop dictation" : "Dictate your message")"
                            aria-pressed="@_isListening"
                            title="@(_isListening ? "Listening â€” click to stop" : "Dictate your message")"
                            disabled="@_isSending"
                            @onclick="ToggleDictationAsync">
```

That `â€”` is verbatim from the file — see S3. `GetMicClass()` (line 460) is the entire visual
listening state: `_isListening ? "ats-assistant-mic is-listening" : "ats-assistant-mic"`.

`ToggleDictationAsync` (line 138) returns early if already listening (delegating to
`StopDictationAsync`), otherwise:

```csharp
		_componentReference ??= DotNetObjectReference.Create(this);

		// Anything already in the box is kept, and dictation continues from it.
		_committedMessage = _currentMessage;

		bool started;

		try
		{
			started = await _dictationModule.InvokeAsync<bool>("start", _componentReference, null);
		}
		catch (JSException)
		{
			started = false;
		}

		if (!started)
		{
			Snackbar.Add("Dictation could not start. Check that this site is allowed to use your microphone.", Severity.Warning);

			return;
		}

		_isListening = true;
		_dictationStatus = "Listening.";
```

`_componentReference ??=` is lazy on purpose: a user who never touches the mic never allocates a
`DotNetObjectReference`, so nothing leaks on the common path. The `null` is the language (§2.3).
And `_isListening = true` (line 178) is set as soon as `start()` returns — **before** the browser
has shown, let alone had answered, the permission prompt (S1).

### 2.3 JS `start()` — building the recognizer

`voiceDictation.js:8-15` is the module's entire global state:

```javascript
// One composer per page, so a single module-level session is enough.
let session = null;

const recognizerFactory = () =>
    window.SpeechRecognition || window.webkitSpeechRecognition;

export function isSupported() {
    return !!recognizerFactory();
}
```

That truthiness test is the **only** support check in the feature — no secure-context test, no
`navigator.mediaDevices` probe, nothing that distinguishes "Firefox" from "served over `http://`".
`start(componentReference, language)` (line 17) bails to `return false` if either the constructor or
the reference is missing, calls `stop()` first (*"A previous session would keep its own handlers
alive and double every result"*), then configures the recognizer:

```javascript
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.maxAlternatives = 1;
    recognition.lang = language || navigator.language || "en-US";
```

That `stop()` makes the single-session design safe for one component and hazardous for two
(**C7**, §4.2). `maxAlternatives = 1` is what makes `result[0]` the only candidate worth reading.
And since C# passes `null`, the locale is the **browser's** — nothing in the repo pins it (S6).

### 2.4 `onresult` → the two strings

The JS handler walks `event.results` from `event.resultIndex`, concatenating `result[0]?.transcript`
into `finalText` or `interimText` by `result.isFinal`, returns early if both are empty, then calls
`notify(state, "OnSpeechResultAsync", finalText, interimText)` (line 59). The C# side (line 208) is
where the correctness lives:

```csharp
	[JSInvokable]
	public async Task OnSpeechResultAsync(string finalText, string interimText)
	{
		if (!string.IsNullOrWhiteSpace(finalText))
			_committedMessage = AppendTranscript(_committedMessage, finalText);

		_currentMessage = AppendTranscript(_committedMessage, interimText);

		await InvokeAsync(StateHasChanged);
	}
```

This is why there are two strings. `_committedMessage` holds only finalized text; `_currentMessage`
— bound to the `<input>` — is committed plus the current guess. Every result **rebuilds**
`_currentMessage` from `_committedMessage` rather than appending, which is what stops the guess
being duplicated across the many `onresult` events per utterance.

`AppendTranscript` (line 273) is pure, static, and the one genuinely unit-testable piece here (§9):

```csharp
	private static string AppendTranscript(string existing, string addition)
	{
		var trimmedAddition = addition?.Trim();

		if (string.IsNullOrEmpty(trimmedAddition))
			return existing;

		return string.IsNullOrWhiteSpace(existing)
			? trimmedAddition
			: $"{existing.TrimEnd()} {trimmedAddition}";
	}
```

**Typing wins.** The input carries `@bind:after="OnMessageTyped"` (`razor:295-303`); line 254 is
`private void OnMessageTyped() => _committedMessage = _currentMessage;`, commented *"otherwise the
next transcript would overwrite the correction the user just made by hand."* That protects a
hand-made correction, but it moves the baseline on *any* edit (S7).

### 2.5 `onend` — the Chrome restart

The single most important quirk in the JS file:

```javascript
    recognition.onend = () => {
        // Chrome ends the stream after a pause even with continuous = true, so
        // dictation has to be restarted or it dies the first time the user thinks.
        if (!state.stopRequested && session === state) {
            try {
                recognition.start();
                return;
            } catch {
                // Fall through and report the stop; the button leaves its listening state.
            }
        }

        if (session === state) {
            session = null;
        }

        notify(state, "OnSpeechEndedAsync");
    };
```

The `session === state` guard stops a *superseded* session restarting itself after another component
called `start()`. Remove either guard and dictation either dies on the first pause or resurrects
itself after being stopped. The C# side (line 240) is deliberately inert — `if (!_isListening)
return;` then clears the flag — and that early return is what keeps the auto-restart invisible: when
`onend` restarts, C# is never told.

### 2.6 `onerror` → `DescribeSpeechError`

The JS handler (line 62) reads `event?.error || "unknown"`, sets `state.stopRequested = true`
**unless** the code is `"no-speech"` (*"a lull, not a failure: Chrome raises it and then ends the
stream, and onend restarts us"*), then notifies `OnSpeechErrorAsync`. C# (line 219) returns
immediately on `"no-speech"`, skips the snackbar on `"aborted"`, and otherwise shows
`DescribeSpeechError(code)` before clearing the listening state. The raw browser code crosses the
boundary and the wording lives in C# (line 257), so adding a message is a one-file change:

```csharp
	private static (string Message, Severity Severity) DescribeSpeechError(string code) => code switch
	{
		"not-allowed" or "service-not-allowed" => (
			"Microphone access is blocked. Allow it in your browser's site settings to dictate.",
			Severity.Warning),
		"audio-capture" => (
			"No microphone was found.",
			Severity.Error),
		"network" => (
			"Speech recognition is offline right now.",
			Severity.Warning),
		_ => (
			"Dictation stopped unexpectedly. Please try again.",
			Severity.Warning)
	};
```

`no-speech` returns *before* the listening state is cleared, which is why a lull leaves the button
pulsing; everything else clears it. Note what this method does **not** do: it never calls JS
`stop()`, relying on the browser firing `onend` after `onerror` (S4).

### 2.7 Stopping — three callers, one method

`StopDictationAsync` (line 184) returns early unless `_dictationModule` is non-null **and**
`_isListening`, clears the flag and sets `_dictationStatus = "Dictation stopped."`, then
`InvokeVoidAsync("stop")` (line 194) inside a try with two catches — `JSException` (*"already torn
down by the browser"*) and `JSDisconnectedException` (*"the circuit is gone"*). Those two clauses
look redundant and are **not** (S2).

Three call sites, all of which matter: `ToggleDictationAsync` (`:145`, the button itself),
`SendAsync` (`:307`) and `ClearConversation` (`:430`). `SendAsync` awaits it *before* reading
`_currentMessage` at line 310 — *"so a trailing transcript cannot land in the box after the message
was already sent"* — and that ordering is what makes §2.4's re-baselining safe. JS `stop()` (line
106) is the other half:

```javascript
export function stop() {
    const state = session;

    if (!state) {
        return;
    }

    state.stopRequested = true;
    session = null;

    try {
        state.recognition.stop();
    } catch {
        // Already stopped by the browser; nothing left to do.
    }

    detach(state);
}
```

`stopRequested = true` first, so the `onend` that `recognition.stop()` asynchronously triggers takes
the non-restart branch — and `detach(state)` then nulls the handlers, so `onend` never fires at all.
That is why a clean stop produces no `OnSpeechEndedAsync` callback and no double state change.

### 2.8 Disposal

`razor:11` declares `@implements IAsyncDisposable`; line 566 implements it — unsubscribe
`TypingChanged`, dispose `_cts`, then:

```csharp
			try
			{
				// Releases the microphone even if the user navigated away mid-sentence.
				await _dictationModule.InvokeVoidAsync("destroy");
				await _dictationModule.DisposeAsync();
			}
			catch (JSDisconnectedException) { /* … */ }
			catch (JSException) { /* … */ }

			_dictationModule = null;
		}

		_componentReference?.Dispose();
		_componentReference = null;
```

Order matters: `destroy()` (`:576`) → module dispose (`:577`) → reference dispose (`:591`).
Disposing the module first would make `destroy()` throw and leave the microphone held; disposing the
.NET reference first would make an in-flight callback throw into JS. Note this catch order is
derived-first, the *opposite* of `StopDictationAsync` — both are correct only because the two
exception types are unrelated (S2).

JS `destroy()` (line 125) captures `session` into `state` **before** calling `stop()` (which nulls
it), then calls `recognition.abort()`. `abort()` not `stop()` is right on the way out: `stop()`
flushes a final result, `abort()` discards it.

---

## 3. The interop contract — strings that must independently agree

Nothing here is compile-checked. A rename on either side fails only at runtime, and for the callback
names it fails **silently**, because `notify()` (line 147) swallows the rejection:

```javascript
function notify(state, method, ...args) {
    try {
        // A late browser event can land after the component is disposed, and an
        // unhandled rejection here would surface as a console error.
        state.componentReference.invokeMethodAsync(method, ...args)?.catch(() => { });
    } catch {
        // The .NET reference is gone; the user has navigated away.
    }
}
```

That is correct for the disposed-component case it was written for, and is also what hides a typo'd
method name — there is no logging branch to tell the two apart.

| C# side | JS side | Consequence of a mismatch |
|---|---|---|
| `"./js/ats/voiceDictation.js"` (`:22`) | the real file path | `import` rejects → caught → button hidden + `console.warn` |
| `InvokeAsync<bool>("isSupported")` (`:97`) | `js:13` | same |
| `InvokeAsync<bool>("start", ref, null)` (`:159`) | `js:17` | `JSException` → caught → "could not start" snackbar |
| `InvokeVoidAsync("stop")` (`:194`) | `js:106` | swallowed; mic stays hot until `destroy()` |
| `InvokeVoidAsync("destroy")` (`:576`) | `js:125` | swallowed; mic may stay hot after navigation |
| `[JSInvokable] OnSpeechResultAsync` (`:209`) | `js:59` | **silent** — no text ever reaches the box |
| `[JSInvokable] OnSpeechErrorAsync` (`:220`) | `js:71` | **silent** — errors never surface |
| `[JSInvokable] OnSpeechEndedAsync` (`:241`) | `js:90` | **silent** — button sticks in listening state |
| `GetMicClass()` → `is-listening` (`:460`) | `css:756` | no visual listening state |
| `_dictationStatus` → live region (`razor:342`) | — | screen readers announce nothing |

The `[JSInvokable]` methods must stay `public` — JS invokes them by name through the
`DotNetObjectReference`. Making them `private` compiles and breaks dictation.

---

## 4. Lifecycle

### 4.1 Does this repeat the `NewOrderComponent` leak? No.

`docs/reviews/ats-oneplatform-fix-details.md` item 9 records that `NewOrderComponent` subscribed to a
singleton service's event in `OnInitializedAsync` with no `Dispose`, so every visit left another
subscription holding a disposed component. Point by point, this feature does not repeat it:

| Hazard | Present? | Evidence |
|---|---|---|
| Dangling .NET event subscription | **No** | `TypingChanged +=` at `:58`, `-=` at `:568` in `DisposeAsync` |
| Dangling JS `.on*` handlers | **No** | `detach(state)` (`js:141`) nulls all three; called by `stop()` and transitively by `destroy()` |
| Uncancelled recognition on navigation | **No** | `DisposeAsync` → `destroy()` → `stop()` + `recognition.abort()` |
| Undisposed `DotNetObjectReference` | **No** | `:591-592` |
| Undisposed `IJSObjectReference` | **No** | `:577` |
| Callback into a disposed reference | **No** | `notify()`'s `?.catch(() => {})` plus `try/catch` absorb the race |

`InitializeDictationAsync` runs only on `firstRender`, so a re-render never re-imports;
`_componentReference ??=` creates the reference at most once; `_cts` is disposed at `:569` and
replaced per send at `:319-320` (unused by the dictation path).

### 4.2 The real hazard: two components on one page

`start()` begins with `stop()`, which calls `detach()` on the previous session — nulling its
handlers including `onend`, so the previous component is **never** notified. If a second
dictation-enabled component is ever added to the same page: A is listening; B calls `start()`;
`detach(A.state)` runs; A never receives `OnSpeechEndedAsync`, so `_isListening` stays `true` and
**A's button keeps pulsing over a microphone it no longer owns**; and when the user clicks A to stop
it, `StopDictationAsync` calls JS `stop()`, which acts on `session` — i.e. **B's** live session —
and kills it.

Latent today because exactly one component uses the module, but it goes live the moment the design
doc's own "Adding dictation to another input" recipe is followed on a page that already has one. The
fix is to key the module-level session by the component reference instead of holding a single
`session`, or to lift the whole thing into a component that owns its own recognizer.

---

## 5. Support, permissions and secure context

### 5.1 Degradation is silent

`isSupported()` returns `false` on Firefox **and** on any insecure origin, because browsers do not
expose the API there at all — the check cannot tell them apart. When it does, `_isSpeechSupported`
stays `false`, the `@if` at `razor:305` renders nothing, the privacy sentence at `razor:347` is
omitted, and there is **no** snackbar, `console.warn` or telemetry. `LogDictationFailureAsync` is
reachable only from the `catch`, i.e. only when the *import itself* fails.

Hiding the button is right for Firefox — a disabled button advertises a capability the user can
never obtain. But the same path swallows a **misconfiguration** with no diagnostic, which is exactly
what the code comment ("a missing button is otherwise invisible") was trying to avoid. If dictation
stops working for everyone in an environment, the only way to tell "unsupported browser" from "we
are serving over `http://`" is to type `window.SpeechRecognition` into devtools by hand.

### 5.2 HTTPS

`SpeechRecognition` requires a secure context. Both `launchSettings.json` profiles for
`FrontendWebassembly` — including the one literally named `"https"` — set
`"applicationUrl": "http://localhost:5134"`, so there is **no HTTPS dev endpoint**; dev works only
because `localhost` is special-cased as a secure context. `appsettings.Sandbox/UAT/Production.json`
point at `https://dev-`/`uat-`/`oneplatform.cibi.com.ph`, but those are **API** bases, not the origin
the WASM app is served from. The repo has no reverse-proxy or TLS-termination config (`docker/` holds
only a Postgres init script; `docker-compose.override.yml` mounts `${APPDATA}/ASP.NET/Https` for the
API containers, not the frontend), so it carries no evidence either way about the deployed page
origin. If the frontend were ever fronted over plain `http://` on a real hostname, the button would
vanish and — per §5.1 — nothing would say why.

### 5.3 Permission flow

There is no explicit `getUserMedia` call. Permission is requested implicitly by
`recognition.start()`, so the prompt appears on first click and the browser remembers the answer per
site. Denied or revoked → `onerror` with `not-allowed` (or `service-not-allowed` if the speech
*service* is blocked) → Warning snackbar and the listening state clears, so mid-session revocation is
handled. No microphone → `audio-capture`, the table's only `Severity.Error`. There is no pre-flight
permission read (`navigator.permissions.query({ name: 'microphone' })` is never called), so a
previously-denied user gets the same first click as a new one and learns only from the snackbar
afterwards (S8).

---

## 6. Can voice reach a write path?

**No — not without two separate explicit clicks.** Worth answering precisely, because the assistant
*can* stage a real order (`ConfirmDraftAsync`, `.razor.cs:358`, which creates an order and sends a
candidate invitation email).

1. `OnSpeechResultAsync` writes `_committedMessage` and `_currentMessage` and calls
   `StateHasChanged`. It does **not** call `SendAsync`. There is no timer, no
   silence-completes-the-message hook, no `onresult`-triggered send anywhere in the file.
2. Dictation cannot synthesise the Enter key. `HandleKeyDown` (line 285) fires on the input's
   `@onkeydown`; recognition writes to the bound value, it does not dispatch key events.
3. A sent message only produces a **staged draft**. Its confirm card's primary button is a real
   `@onclick="@(() => ConfirmDraftAsync(message))"`, and that method opens a dialog first —
   `await DialogService.ShowAsync<YesNoDialogComponent>(…)`, then `if (result?.Canceled != false)
   return;`. So an accidentally-spoken phrase reaches, at worst, a draft card on screen. Creating an
   order needs **Send**, then **Confirm & send**, then **Proceed**.
4. Dictated text is HTML-encoded before rendering — `ChatMessage.FromUser` uses
   `System.Net.WebUtility.HtmlEncode(text)` — so spoken markup cannot inject into the transcript.
   `MarkupString` is used only for assistant output (Markdig over the model's reply), never for
   `_currentMessage`.

The invariant to protect: **`OnSpeechResultAsync` must never call `SendAsync`.** It is the entire
reason misrecognition is safe here, and it is invisible — enforced by the absence of a call, not by
a guard.

---

## 7. UI states

| State | Exists? | Where |
|---|---|---|
| Idle | Yes | `.ats-assistant-mic`, ghost styling mirroring `.ats-assistant-clear` (`css:721`) |
| Listening | Yes | `.is-listening` fill + pulse (`css:756`), `aria-pressed="true"`, placeholder `"Listening"`, `_dictationStatus = "Listening."` |
| Interim transcript | Yes | the guess is just the current `_currentMessage` — plain text, no separate styling |
| Sending (mic disabled) | Yes | `disabled="@_isSending"`; `.ats-assistant-mic:disabled` (`css:751`). Not in the design doc |
| Error / denied | Yes | Snackbar via `DescribeSpeechError`; `not-allowed` also clears the listening state |
| Unsupported | Yes | button not rendered (`razor:305`) |
| No-speech | **No — deliberate** | `if (code is "no-speech") return;` (`:223`); button stays pulsing with zero indication nothing was heard |
| Permission prompt pending | **No** | indistinguishable from Listening (S1) |
| Unsupported/insecure *diagnostic* | **No** | §5.1 — no log, no message |
| Loading / starting | **No** | no busy state between click and `start()` returning |

Against the guide's loading/empty/validation/success/failure requirement: **empty** (the input just
holds prior text), **success** (text appears) and **failure** (snackbars) are covered. **Loading** is
not — and that gap matters, because the window it would cover is exactly the permission prompt.
**Validation** does not apply; dictated text is not validated. The `no-speech` silence is defensible
(a pause is normal; a warning per pause would be noise), but a user who speaks and is not heard gets
no signal beyond "the words did not appear".

### 7.1 Accessibility

All verified present: `aria-pressed` plus state-dependent `aria-label`/`title` (`razor:309-311`); a
visually hidden `<span class="ats-assistant-sr-only" role="status" aria-live="polite">` carrying
`@_dictationStatus` (`razor:341-343`, clipped by `css:782`); a real `<button>`, so Tab reaches it and
Enter/Space toggle it; and reduced motion (`css:842`), which sets `transition-duration` and
`animation-duration` to `0.01ms !important` and `animation-iteration-count: 1 !important` across
`.ats-assistant-page *` and its pseudo-elements. That does neutralize the pulse (**C5**), leaving the
solid `--c-chip-solid-bg` fill and the `aria-live` announcement as the only cues — probably adequate,
since the fill change is a strong non-motion signal, but a reduction rather than a substitution.

---

## 8. Theming and responsiveness

### 8.1 Tokens

The `/* Dictation */` section is largely compliant. `.ats-assistant-mic` uses the file's alias tokens
(`--border-strong`, `--text-2`, `--blue-500`, `--blue-tint`, `--blue-600`), all re-pointed at `--c-*`
in the `.ats-assistant-page` scope (`css:1-22`), and `.is-listening` (`css:756`) uses the
non-inverting tokens correctly — `background: var(--c-chip-solid-bg)` and `color: var(--c-on-dark-fg)`.
`--c-chip-solid-bg` is `#0b1b3d` light / `#2b3d54` dark and `--c-on-dark-fg` is white in both, so the
fill **does** follow dark mode.

**The pulse does not** (**C6**). `css:768-780` runs `box-shadow: 0 0 0 0 rgba(29, 95, 209, .45)` at
`0%` out to `0 0 0 8px rgba(29, 95, 209, 0)` at `70%`. That `rgba(29, 95, 209, …)` is exactly
`#1d5fd1` — the second stop of `--c-primary-gradient` in **light** mode (`theme.css:121`). Dark mode
redefines that gradient to `linear-gradient(120deg, #16294c, #2560bd)` (`theme.css:248`), so a
dark-mode user sees a dark listening chip radiating a light-mode blue halo. That violates
`docs/ui-theming-and-responsiveness.md`'s rule that "`wwwroot/css/theme.css` is **the only place a
colour literal is allowed to live**", and no existing token fits — the `.45`→`0` alpha ramp needs a
dedicated token pair or a `color-mix()` off a `--c-primary-*` token. The file already has nine other
`rgba()` literals (lines 35, 194, 201, 227, 343, 615, 619, 663, 694), so this is a pre-existing
pattern the feature extended rather than introduced — but the pulse is this feature's own.

Related, pre-existing, and it affects the new button: `css:804` hardcodes
`outline: 3px solid rgba(46, 124, 224, .25)` for `.ats-assistant-page button:focus-visible`.
`--c-focus-ring` already exists with exactly that light value (`theme.css:107`) **and** a dark value
`rgba(110, 168, 255, 0.35)` (`theme.css:240`), so using the literal means the mic's focus ring keeps
its light-mode colour in dark mode.

### 8.2 Responsiveness at 390px

The composer row is `.ats-assistant-input-wrap` (`css:650-660`): `display: flex; gap: 10px` with **no
`flex-wrap`**, holding the input (`flex: 1 1 auto; min-width: 0`, `css:666`), the mic (`40px`,
`flex: 0 0 auto`) and send (`40px`, `flex: 0 0 auto`).

The guide's checklist asks for `flex-wrap: wrap` on flex rows holding buttons. It is absent, and that
is the **right** call — wrapping the mic under the input would look broken. What actually makes 390px
work is `min-width: 0` on the input, letting it shrink below its content width: 390 − 32 (composer
padding at ≤960px) − 21 (`5px 5px 5px 16px` wrap padding) − 20 (two gaps) − 80 (two buttons) ≈
**237px** for the input. It fits.

Neither the `@media (max-width: 960px)` block (`css:810`) nor the `@media (max-width: 600px)` block
(`css:827`) touches the composer — the first trims padding, the second hides the brand subtitle and
restacks the confirm-card buttons. So the composer's narrow-viewport behaviour is pure flex,
unmediated by any query (**C8**): the design doc's "≤600px" test does not test a media query, and
neither test exercises the 601–960px band the guide calls out. The Dictation CSS adds no `min-width`
to a table root and no `overflow-x: hidden`, so it trips neither prohibition.

---

## 9. Tests

**There is no automated coverage for this feature, and for most of it none is realistically
possible.** Stated plainly rather than implied: no `.csproj` in the repo references bUnit or
Playwright (grep across all `*.csproj`: no matches), so there is no component-test infrastructure to
hang a test on; the Web Speech API needs a real browser and a real microphone, and `Testcontainers`
covers Postgres, not Chrome; and nothing under `Test/` mentions `AIAssistant`, `Dictation` or
`voiceDictation`.

`Test/Test/Test.csproj` *does* reference `FrontendWebassembly.csproj` (line 59), contrary to **C3**,
and `Test/Test/UI/CsvTextDecoderTests.cs` and `CsvPreviewParserTests.cs` already unit-test plain
FrontendWebassembly helpers — so the precedent for testing UI-side pure C# exists and has simply not
been used here.

**Testable and untested:** `AppendTranscript` (`:273`) is a pure static string function with real edge
cases — null addition, whitespace-only addition, space collapsing, empty existing — and is exactly
the shape of `CsvPreviewParser`, so it belongs in `Test/Test/UI/`. `DescribeSpeechError` (`:257`) is
likewise a pure static switch worth a table test, since its `_` arm catches every browser error code
nobody enumerated.

**Verified instead:** the design doc's ten manual checks, which are the *only* verification. Items 5,
6 and 7 (deny, Firefox, navigate-while-listening) each exercise a different branch of §4 and §5.
Build at time of writing: `dotnet build UI/FrontendWebassembly/FrontendWebassembly.csproj` succeeds,
0 errors, 51 warnings — all pre-existing, and none in the dictation code except `CS0108` on
`AIAssistantComponent.razor(8,19)` (`@inject ISnackbar Snackbar` hides `CrudPageBase.Snackbar`).

---

## Sharp edges

**S1 — The pulse means "`start()` returned true", not "we are recording."** `_isListening = true` is
set immediately after the interop call (`:178`), but the browser shows its permission prompt *after*
`recognition.start()` returns. During that window the button pulses and the live region says
"Listening." while no audio is captured. A distinct "waiting for permission" state would need
`navigator.permissions.query` — the Web Speech API exposes no prompt signal.

**S2 — The two `catch` clauses in `StopDictationAsync` look redundant and are not.** Verified against
`Microsoft.JSInterop 10.0.8`: `JSDisconnectedException` derives from `System.Exception`, **not** from
`JSException` (`BaseType` printed as `System.Exception`; a full rebuild emits no `CS0160`). Deleting
the `JSDisconnectedException` clause would let a navigation-during-stop escape unhandled.
`DisposeAsync` catches them in the opposite order and is equally correct — ordering only matters when
the types are related, and they are not.

**S3 — The mic tooltip contains mojibake.** `AIAssistantComponent.razor:311` stores a double-encoded
em-dash (`â€”` on disk, not `—`), so the tooltip a dictating user sees reads "Listening â€” click to
stop". The same file is internally inconsistent — the audit-table dashes render correctly as `—` —
and five more instances survive at lines 105, 108, 109, 110 and 248 (`Sendingâ€¦`). The `.razor.cs`
and the CSS are clean. Line 311 is this feature's own; retype the dash rather than copying the
existing bytes.

**S4 — `OnSpeechErrorAsync` never tells JS to stop.** It sets `_isListening = false` (`:234`) and
returns, relying on the browser firing `onend` after `onerror` so JS clears its own `session`. Chrome
does. On a browser that raises an error without a following `end`, the module-level `session`
survives with live handlers until the next `start()` or `destroy()`, while C# says `false`.
Self-healing rather than leaking, but the two sides disagree until something resets them.

**S5 — `session` is module-scoped and singular.** See §4.2: one recognizer per page, and the failure
mode for a second consumer is a stuck pulsing button plus cross-talk, not an error.

**S6 — Recognition language is the browser's, never the app's.** `start()` receives `null` and falls
through to `navigator.language`. No setting, no `en-PH` default, no way for a requestor to change it
from the UI. For a system whose users dictate Filipino names and addresses this is the likeliest
source of "it misheard the candidate's name" reports, and it is invisible in the code.

**S7 — `_committedMessage` is re-baselined on every keystroke.** `OnMessageTyped` (`:254`) copies
`_currentMessage` wholesale, including any interim guess on screen. Select-all + delete while the mic
is open commits the empty string, which is correct — but so does typing over a partial guess, which
silently promotes a half-recognized word into the committed baseline.

**S8 — No pre-flight permission read.** A user who denied the microphone last month gets an identical
first click to a new user, and learns only from the Warning snackbar afterwards.

---

## Wiring

No DI registration, no gateway route, no `ATSPaths.cs` entry, no backend or database change — the
feature lives entirely inside `UI/FrontendWebassembly`. What *is* wired, and invisibly:

| Thing | Side A | Side B |
|---|---|---|
| Module *not* bundled | `wwwroot/index.html:141-156` | must stay absent; no `#[.{fingerprint}]` marker |
| JS function names | `.razor.cs:97`, `:159`, `:194`, `:576` | `js:13`, `:17`, `:106`, `:125` |
| Callback names | `.razor.cs:209`, `:220`, `:241` | `js:59`, `:71`, `:90` |
| `is-listening` class | `.razor.cs:460` `GetMicClass()` | `css:756`, `css:763` |
| `_dictationStatus` | `.razor.cs:45`, written `:179`, `:190`, `:235`, `:247` | `razor:342` |
| `_isSpeechSupported` | `.razor.cs:43`, written `:97`, `:104` | `razor:305`, `razor:347` |
| Page permission gate | `razor:4-5` `[RequirePermission(6, 7)]`, `[RequireATSModule(12)]` | ATS application/submenu seed data |

The last row is inherited from the assistant page rather than added by this feature, but it gates the
mic button: a user without ATS application 6 / submenu 7 never renders the composer at all.

---

## Change X, also check Y

| If you change… | …also check |
|---|---|
| Any `[JSInvokable]` name or signature | the matching `notify(state, "…")` in `voiceDictation.js` (§3). Must stay `public`; a mismatch is **silent** |
| Any JS `export function` name | the matching C# string literal (§3) |
| `DictationModulePath` | the real file location, and that it stays out of `index.html` (§1) |
| The `onend` restart branch | §2.5 — both the `stopRequested` and `session === state` guards |
| `detach(state)` | §4.2 and S5 — it is what silences the previous session, for better and worse |
| `AppendTranscript` or `OnMessageTyped` | §2.4 and S7 — one is called for both final and interim, the other re-baselines on *every* keystroke |
| `StopDictationAsync`'s early-return guard | its three callers (`:145`, `:307`, `:430`) and S4 |
| Either `catch` in `StopDictationAsync` or `DisposeAsync` | S2 — `JSDisconnectedException` is **not** a `JSException` |
| `DescribeSpeechError` | §2.6 — the `_` arm is the catch-all; `no-speech`/`aborted` are filtered *before* it |
| `GetMicClass()` or `is-listening` | `css:756` and `css:763` |
| The pulse keyframes | §8.1 — add a token pair, not another `rgba()` literal |
| The composer flex row | §8.2 — `min-width: 0` on `.ats-assistant-input` is load-bearing at 390px |
| The `@if (_isSpeechSupported)` guard | `razor:347` too — the privacy sentence is behind a second copy of the same condition |
| `_dictationStatus` strings | the `razor:342` live region — announced verbatim by screen readers |
| `DisposeAsync`'s ordering | §2.8 — `destroy()` → module dispose → reference dispose |
| Adding a second dictation component | §4.2 first — the module holds one session; fix that before adding the caller |
| The mic `title` attribute | S3 — retype the em-dash |

---

*Verified against the code on branch `feature/Update-ReadMe-File`. Build check:
`dotnet build UI/FrontendWebassembly/FrontendWebassembly.csproj` — succeeded, 0 errors.*
