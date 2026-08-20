// Browser speech-to-text for the ATS assistant composer.
//
// Blazor has no speech API of its own; this wraps the browser's Web Speech API and
// pushes transcripts back to AIAssistantComponent through [JSInvokable] callbacks.
// Loaded on demand with import(), like safeSignaturePad.js, so it is not in index.html.

// One composer per page, so a single module-level session is enough.
let session = null;

const recognizerFactory = () =>
    window.SpeechRecognition || window.webkitSpeechRecognition;

export function isSupported() {
    return !!recognizerFactory();
}

export function start(componentReference, language) {
    const Recognizer = recognizerFactory();

    if (!Recognizer || !componentReference) {
        return false;
    }

    // A previous session would keep its own handlers alive and double every result.
    stop();

    const recognition = new Recognizer();

    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.maxAlternatives = 1;
    recognition.lang = language || navigator.language || "en-US";

    const state = {
        componentReference,
        recognition,
        stopRequested: false
    };

    recognition.onresult = event => {
        let finalText = "";
        let interimText = "";

        for (let index = event.resultIndex; index < event.results.length; index++) {
            const result = event.results[index];
            const transcript = result[0]?.transcript ?? "";

            if (result.isFinal) {
                finalText += transcript;
            } else {
                interimText += transcript;
            }
        }

        if (!finalText && !interimText) {
            return;
        }

        notify(state, "OnSpeechResultAsync", finalText, interimText);
    };

    recognition.onerror = event => {
        const code = event?.error || "unknown";

        // "no-speech" is a lull, not a failure: Chrome raises it and then ends the
        // stream, and onend restarts us. Anything else ends the session.
        if (code !== "no-speech") {
            state.stopRequested = true;
        }

        notify(state, "OnSpeechErrorAsync", code);
    };

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

    try {
        recognition.start();
    } catch {
        detach(state);

        return false;
    }

    session = state;

    return true;
}

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

export function destroy() {
    const state = session;

    stop();

    if (!state) {
        return;
    }

    try {
        state.recognition.abort();
    } catch {
        // The page is going away; releasing the microphone is best effort.
    }
}

function detach(state) {
    state.recognition.onresult = null;
    state.recognition.onerror = null;
    state.recognition.onend = null;
}

function notify(state, method, ...args) {
    try {
        // A late browser event can land after the component is disposed, and an
        // unhandled rejection here would surface as a console error.
        state.componentReference.invokeMethodAsync(method, ...args)?.catch(() => { });
    } catch {
        // The .NET reference is gone; the user has navigated away.
    }
}
