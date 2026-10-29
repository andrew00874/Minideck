# Local translation validation

Validated on Windows with an AMD RX 9060 XT using the official llama.cpp b11960 Vulkan runtime and pinned Tencent Hy-MT2 weights. These are smoke tests, not a translation-quality benchmark or a claim of compatibility with every keyboard.

## Code review changes

- Preserve the existing Papago/direct-editor routing and remove the old shared 2,000-character cap while integrating immediate local translation.
- Split long selections into bounded chunks, preserve line separators and boundary whitespace, and keep all output transient.
- Reject incomplete or output-limited model responses rather than presenting a partial translation as complete.
- Add a separate, checksum-verified model/runtime installer; automatically start its authenticated loopback server and stop only the process MiniDeck owns. Coordinate startup and shutdown so closing during startup cannot launch an orphan server.
- Keep custom Ollama and OpenAI-compatible endpoints configurable. There is no cloud fallback.

## Validation performed

- Korean and English Windows builds and all 17 existing/extended app self-test groups passed locally. This includes real-control UI Automation selection capture, global hotkey registration/routing, settings migration, dial behavior and translation result lifecycle tests.
- HTTP integration tests passed for Ollama and OpenAI-compatible model discovery, Unicode, cancellation, rejected endpoints/redirects, bounded responses, multi-chunk selections, preserved line breaks, and incomplete-output errors.
- English coverage passed for 201 user-facing strings. Browser routing and native-message framing/limit checks passed.
- Six synthetic examples per model covered technical wording, idioms, instructions treated as source text, Japanese → Korean and Korean → English. The 1.8B Q8_0 model mistranslated “race condition”; the 7B Q4_K_M model used the appropriate technical term and handled the reviewed idiom more naturally. The 7B model was selected as the default.
- The six short 7B requests took approximately 0.7–2.0 seconds with a loaded model. Through the actual MiniDeck client and managed runtime, a cold short request took about 6.9 seconds; a 50-character multiline selection preserved all five line breaks, and a 2,505-character prose selection completed in about 26.8 seconds. Timing varies with text, model state and hardware.
- A deliberately repetitive long example exhausted the model's output budget and was correctly rejected as incomplete. No claim is made that every accepted selection will yield a complete or accurate model response.

Desktop Computer Use testing was stopped by the user with Escape before the real editor shortcut flow completed. Physical key-3 activation and broad compatibility with arbitrary/elevated editors remain unverified for this feature. App tests, actual model inference and installed settings are separate evidence from a physical-key acceptance test.

## Package scope

Development packages are versioned 0.3.0-beta.1 and include `scripts/setup-local-translation.ps1`. The existing 0.2.0-beta.1 release downloads remain unchanged. Model weights are downloaded separately into local application data and are not included in the application ZIP.
