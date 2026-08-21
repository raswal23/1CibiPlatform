# ATS AI Assistant â€” Voice input

The assistant's composer has a microphone button. Press it, talk, and the words appear in the
text box as you speak. You read what was captured and press **Send** yourself.

That is the whole feature. It deliberately does **not**:

- auto-send when you stop talking â€” the assistant can stage a real order, so a misheard
  sentence must never reach the confirmation card on its own;
- read replies back aloud;
- send audio to 1CibiPlatform. There is no endpoint, no gateway route and no database change.

## There is no "Blazor speech-to-text"

Neither Blazor nor MudBlazor ships a speech API. What exists is the **browser's** Web Speech API
(`SpeechRecognition`, still `webkitSpeechRecognition` in Chrome/Edge), which a Blazor WebAssembly
app reaches through JS interop.

We write that interop by hand rather than taking a community NuGet wrapper. The repo already has
the pattern â€” `wwwroot/js/generic/safeSignaturePad.js` â€” and a wrapper package would add a
dependency that nothing else uses while still needing the same lifecycle handling.

## Architecture

```text
AIAssistantComponent.razor          mic button, listening state, aria-live status
  -> AIAssistantComponent.razor.cs  ToggleDictationAsync
  -> IJSObjectReference             import("./js/ats/voiceDictation.js")
  -> voiceDictation.js              start / stop / destroy
  -> window.SpeechRecognition       browser recognizer

  and back:

window.SpeechRecognition
  -> voiceDictation.js onresult / onerror / onend
  -> DotNetObjectReference
  -> [JSInvokable] OnSpeechResultAsync / OnSpeechErrorAsync / OnSpeechEndedAsync
  -> _currentMessage                the bound value of the composer input
```

Key files:

| Concern | File |
|---|---|
| Recognizer lifecycle | `UI/FrontendWebassembly/wwwroot/js/ats/voiceDictation.js` |
| Button and status region | `UI/FrontendWebassembly/Component/ATS/AIAssistant/AIAssistantComponent.razor` |
| State, interop, callbacks | `UI/FrontendWebassembly/Component/ATS/AIAssistant/AIAssistantComponent.razor.cs` |
| Mic styling | `UI/FrontendWebassembly/Component/ATS/AIAssistant/AIAssistantComponent.razor.css` |

Nothing was added to `index.html`, `FrontendWebassembly.csproj`, `FrontendServiceConfig.cs`, or
any backend/gateway file.

## The module is loaded on demand

`voiceDictation.js` is an ES module (`export function`) loaded with
`JS.InvokeAsync<IJSObjectReference>("import", "./js/ats/voiceDictation.js")` on first render.

It is intentionally **not** a `<script>` tag in `index.html`, unlike `atsAssistant.js` and the
other `window.*` helpers. Only users who open the assistant pay for the download. This follows
`safeSignaturePad.js`, which is absent from `index.html` for the same reason.

Because it is imported rather than fingerprinted through the `index.html` placeholder pass, the
path is the literal file name â€” do not add a `#[.{fingerprint}]` marker to the import string.

## Browser support

| Browser | Dictation |
|---|---|
| Chrome, Edge | Yes |
| Safari (macOS/iOS) | Yes |
| Firefox | No â€” `SpeechRecognition` is not implemented |

`isSupported()` is checked on first render and the button is **hidden** when it returns false,
so Firefox users see the composer exactly as it was before this feature. A disabled button would
have been worse: it advertises a capability the user can never get, with no action to fix it.

## Privacy

Chrome and Edge stream the audio to Google's speech service to transcribe it. Safari uses
Apple's. **No audio reaches 1CibiPlatform servers, and nothing is recorded or stored by us** â€”
only the resulting text, and only once the requestor presses Send.

This is worth stating plainly because requestors dictate candidate names and email addresses.
The composer carries a visible note: *"Dictation is handled by your browser and is never sent to
us."* If a client ever requires that no third party sees the audio, the feature has to move to
server-side transcription; the browser API cannot be made local-only.

The browser asks for microphone permission the first time, and remembers the answer per site.

## Behavior details

**Interim results.** `interimResults = true`, so the greyed-in guess appears while you are still
speaking and firms up when the browser finalizes the phrase. The component keeps two strings:
`_committedMessage` (finalized) and `_currentMessage` (committed + the current guess). The next
result replaces the guess instead of appending it twice.

**Typing wins.** `@bind:after="OnMessageTyped"` re-baselines `_committedMessage` whenever you
edit the box by hand, so a late transcript cannot clobber a correction you just typed.

**Chrome ends the stream on a pause.** Even with `continuous = true`, Chrome fires `onend` after
a few seconds of silence. `voiceDictation.js` restarts the recognizer in `onend` unless the stop
was requested, so dictation survives you pausing to think. This is the single most important
quirk in the file â€” remove that restart and dictation appears to "randomly stop".

**Sending stops the mic.** `SendAsync` awaits `StopDictationAsync()` first, so a trailing
transcript cannot land in an already-emptied box. `Clear` does the same.

**Errors.** The JS passes the raw `SpeechRecognitionErrorEvent.error` code through; the wording
lives in C# (`DescribeSpeechError`):

| Code | Surfaced as |
|---|---|
| `not-allowed`, `service-not-allowed` | Warning â€” "Microphone access is blocked. Allow it in your browser's site settings to dictate." |
| `audio-capture` | Error â€” "No microphone was found." |
| `network` | Warning â€” "Speech recognition is offline right now." |
| `no-speech` | *nothing* â€” a pause is normal; the browser restarts itself |
| `aborted` | *nothing* â€” the user pressed the button |
| anything else | Warning â€” "Dictation stopped unexpectedly. Please try again." |

Everything except `no-speech` also drops the button out of its listening state.

**Disposal.** The component is now `IAsyncDisposable` (it was `IDisposable`). `DisposeAsync`
calls `destroy()` so the microphone is released even if the user navigates away mid-sentence,
then disposes the module and the `DotNetObjectReference`. `JSDisconnectedException` and
`JSException` are swallowed there â€” the page is already going away.

## Accessibility

- The button carries `aria-pressed` and an `aria-label`/`title` that change with state.
- A visually hidden `role="status" aria-live="polite"` region in the composer announces
  "Listening." and "Dictation stopped." for screen readers, since the visual cue is a colour and
  a pulse.
- The pulse animation is neutralized by the page's existing
  `@media (prefers-reduced-motion: reduce)` block, which targets `.ats-assistant-page *`.
- The button is a real `<button>`, so Tab reaches it and Enter/Space toggle it, and the page's
  `:focus-visible` ring applies.

## Adding dictation to another input

The interop is not a shared service on purpose â€” this is one composer, and a premature
abstraction would have to guess at the second caller's needs. To reuse it:

1. Import the module in `OnAfterRenderAsync(firstRender)` and store `isSupported()`.
2. Create a `DotNetObjectReference` and add `[JSInvokable] OnSpeechResultAsync(final, interim)`,
   `OnSpeechErrorAsync(code)` and `OnSpeechEndedAsync()`.
3. Make the component `IAsyncDisposable` and call `destroy()`.

If a third screen needs it, that is the point to lift steps 1â€“3 into a shared
`VoiceDictation` component under `Component/Generic` â€” not before.

Note that the module holds **one** recognizer at module scope. Two components dictating at once
on the same page would fight over it; `start()` stops any previous session first, so the last
one to start wins rather than both receiving results.

## Tests

There is no bUnit/UI test project in this repository (`Test/Test/Test.csproj` covers BackendAPI
only), and the Web Speech API needs a real microphone and a real browser, so this feature is
verified by build plus manual checks:

1. Chrome/Edge â€” click the mic, allow the prompt, speak; words appear while you talk.
2. Send â€” the mic stops and the message goes out as shown.
3. Speak, then correct the text by hand before sending â€” the edit survives.
4. Pause for more than five seconds and keep talking â€” dictation continues.
5. Deny the permission â€” Warning snackbar, button returns to idle, no console error.
6. Firefox â€” no mic button; the composer is otherwise unchanged.
7. Navigate away while listening â€” no console errors and the browser's recording indicator clears.
8. Keyboard only â€” Tab reaches the button, Enter/Space toggles, focus ring visible.
9. Narrow viewport (â‰¤600px) â€” the composer still fits both buttons.
10. **Clear** while listening â€” dictation stops.
