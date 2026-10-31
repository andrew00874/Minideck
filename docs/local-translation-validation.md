# Local translation validation

Validated on Windows with an AMD RX 9060 XT using the official llama.cpp b11960 Vulkan runtime and pinned Tencent Hy-MT2 weights. These are smoke tests, not a translation-quality benchmark or a claim of compatibility with every keyboard.

## Code review changes

- Preserve the existing Papago/direct-editor routing and remove the old shared 2,000-character cap while integrating immediate local translation.
- Split long selections into bounded chunks, preserve line separators and boundary whitespace, and keep all output transient.
- Reject incomplete or output-limited model responses rather than presenting a partial translation as complete.
- Add a separate, checksum-verified model/runtime installer; prepare its authenticated loopback server in the background and stop only the process MiniDeck owns. Coordinate startup and shutdown so closing during startup cannot launch an orphan server.
- Keep custom Ollama and OpenAI-compatible endpoints configurable. There is no cloud fallback.

## Validation performed

- Korean and English Windows builds and all 17 existing/extended app self-test groups passed locally. This includes real-control UI Automation selection capture, global hotkey registration/routing, settings migration, dial behavior and translation result lifecycle tests.
- HTTP integration tests passed for Ollama and OpenAI-compatible model discovery, Unicode, cancellation, rejected endpoints/redirects, bounded responses, multi-chunk selections, preserved line breaks, and incomplete-output errors.
- English coverage passed for 201 user-facing strings. Browser routing and native-message framing/limit checks passed.
- Six synthetic examples per model covered technical wording, idioms, instructions treated as source text, Japanese → Korean and Korean → English. The 1.8B Q8_0 model mistranslated “race condition”; the 7B Q4_K_M model used the appropriate technical term and handled the reviewed idiom more naturally. The 7B model was selected as the default.
- The six short 7B requests took approximately 0.7–2.0 seconds with a loaded model. Through the actual MiniDeck client and managed runtime, a cold short request took about 6.9 seconds; a 50-character multiline selection preserved all five line breaks, and a 2,505-character prose selection completed in about 26.8 seconds. Timing varies with text, model state and hardware.
- A deliberately repetitive long example exhausted the model's output budget and was correctly rejected as incomplete. No claim is made that every accepted selection will yield a complete or accurate model response.

Desktop Computer Use testing was stopped by the user with Escape before the real editor shortcut flow completed. Physical key-3 activation and broad compatibility with arbitrary/elevated editors remain unverified for this feature. App tests, actual model inference and installed settings are separate evidence from a physical-key acceptance test.

## Latency follow-up

The same Hy-MT2 7B weights and inference settings are retained. The result window now consumes incremental OpenAI-compatible SSE/Ollama NDJSON output. Streaming integration checks cover early partial output before the final frame, Unicode, cancellation, bounded frames, incomplete terminal responses, and JSON fallback. UI lifecycle tests also cover preview visibility and preventing cancelled/incomplete previews from becoming copyable completed results.

For the 2,505-character fixture with the model loaded, the first client preview callback arrived after 0.228 seconds and completion took 24.825 seconds. The previous buffered test showed nothing until approximately 26.8 seconds. These are individual smoke measurements affected by cache and hardware state; streaming improves time to first output and does not remove the model's generation cost. A short request after a sleeping server took 3.334 seconds to first output, demonstrating the separate model-loading delay.

MiniDeck now prepares its installed model during normal startup (not test/preview modes), and keeps it loaded for up to 15 idle minutes. No smaller model or reduced-quality inference profile was substituted.

## Package scope

The stability follow-up uses the result window's UI queue and full text snapshots instead of native append/selection operations. A display-update exception cancels that result while keeping the launcher running. Normal UI errors are also recorded and recover the result window rather than exiting the launcher. App and model lifecycle events are recorded in a bounded local `diagnostics/events.jsonl` file; exception messages, selected text, translations, process-output lines and credentials are excluded. Native errors are reduced to fixed categories such as GPU memory/device loss or model loading. Error-injection and privacy tests cover these boundaries. The original reported crash was not reproduced and its exact cause remains unconfirmed.

Development packages are versioned 0.3.0-beta.4 and include `scripts/setup-local-translation.ps1`. The existing 0.2.0-beta.1 release downloads remain unchanged. Model weights are downloaded separately into local application data and are not included in the application ZIP.

## Managed engine recovery and readable text

The installed engine has an explicit MiniDeck provider. Requests resolve its model and endpoint from the validated installation manifest; an old saved port or model does not silently select an external connection. Legacy OpenAI-compatible settings pointing to the installed endpoint are recognized automatically. Custom Ollama and LM Studio settings keep their existing behavior.

MiniDeck monitors its owned process without readiness polling that would wake a sleeping model. Background recovery permits at most three preparation attempts per five minutes. A failed connection, HTTP 503, or interrupted stream retries the current translation chunk once, after clearing its partial preview. Cancellation and configuration errors do not trigger a retry, and repeated failure remains a visible engine-recovery error. Model output limits still reject incomplete results.

The separate runtime fixture tests stopped-engine startup, stale saved settings, legacy migration, concurrent preparation, a process exit during streaming, clearing partial output, one-retry limits, configuration errors, cancellation, background recovery and its restart budget, and isolation from custom servers. It uses temporary fake model/key files and a separate fixture executable, without real weights, GPU use, or external services.

A local smoke test with the pinned Hy-MT2 7B Vulkan runtime deliberately killed only its own worker after the first streamed preview. The actual MiniDeck client restarted the worker, reset the preview and completed a 328-character Korean result. The full cold-start/crash/reload/generation exercise took 14.304 seconds; this is recovery evidence, not a normal translation-speed measurement or a reproduction of the original crash.

Source and result text are 14pt, with 12pt translation settings and controls. The windows provide additional space for the larger text. Windows text controls receive CRLF line endings for display while translation requests retain the original source; a multiline UI check covers source and result paragraphs. Built-in mode hides server-address controls and displays preparation/recovery feedback. Korean and English UI test runs save rendered translation-window previews for inspection. Physical key-3/editor acceptance remains separate from these checks.
