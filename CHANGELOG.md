# Changelog

## Unreleased

- Include an optional SHA-256-verified Hy-MT2 7B / llama.cpp Vulkan setup. MiniDeck starts its loopback-only, authenticated server on demand, lets the model sleep after five idle minutes, and stops its own server on exit. Installed model weights are kept outside the application package.
- Translate long selections in bounded chunks, preserve original line breaks, report progress and reject incomplete model responses instead of displaying them as completed translations.
- Select text and press key 3 to translate immediately with Ollama or LM Studio on this PC. A nonmodal MiniDeck window shows the source, progress, and translation without an engine-selection or knob-confirmation step.
- Added local connection, installed-model selection and target-language settings. Immediate local translation is enabled by default; disable it to restore the existing web text-action dial. Models must be installed separately, and the existing 0.2.0-beta.1 downloads do not include this feature.
- The result window supports copying, retrying and opening settings. Closing it or pressing Esc cancels the request; a new selection replaces the previous request and result.
- Local translation accepts loopback servers only and does not need Chrome Browser Link or fall back to cloud services. MiniDeck keeps source text and translations in memory only; the model server manages its own logs and caches.
- Add Papago with its official parrot icon. Existing settings receive the preset once while preserving custom apps, engines, language, position and dial size.
- Remove the shared 2,000-character limit. Google Translate and Papago receive text directly in their source editors, with their current 5,000/3,000-character limits. Search keeps an encoded-URL limit; over-limit translations are never silently truncated.
- Browser Link 1.2.0 adds scripting access only for the two translation sites. Reload the extension after replacing the app and host.
- Check long Unicode selections, direct-input routing, navigation races, service limits, and migration that respects later preset deletion.

## 0.2.0-beta.1

- Key 1 opens saved apps, key 2 switches between existing windows, and key 3 searches/translates selected text through the dial. Existing six hardware mappings stay the same.
- Text capture happens before the overlay opens. The original selection is previewed and sent only when an engine is confirmed; cancelling discards it.
- Google, Naver, Bing and Google Translate presets, plus editable URL templates. No API key required.
- Bundled service SVG logos replace placeholder letters in the text-action dial and engine list. Icons are embedded for offline use; custom services use a magnifying-glass icon.
- Browser Link 1.1.0 updates an engine-specific result tab with a new query while normal website entries continue to preserve their current route.
- Update the native host and reload the extension when upgrading. Its new storage permission keeps only result-tab IDs and origins in session memory.
- Added tests for transient selection state, explicit confirmation, URL encoding, window lifetime checks, legacy settings and result-tab routing. The user confirmed that selecting text, pressing key 3, choosing an engine and pressing the knob opens the result page on the connected device.

## 0.1.0-beta.2

- Fix website selection changing the Chrome tab without bringing its window forward on Windows.
- MiniDeck resolves the browser process from its connected native host, grants it foreground access and activates a Chrome window before the extension selects the destination tab/window.
- No extension reload or reinstall is required when updating from beta.1.
- Added tests for direct/native-host-through-cmd process discovery and rejection of unrelated/cyclic process ancestry.

## 0.1.0-beta.1

- Separate English and Korean Windows builds from one source tree.
- Circular app launcher with animated selection, smooth alpha edges, saved drag position and a 280–560 px size setting.
- PNG/SVG custom icons and bundled Pretendard / Inter fonts.
- Three app shortcuts and dial navigation through six global keyboard shortcuts.
- Onboard mapping editor for the identified 1189:8890, Report ID 3 device protocol.
- Optional Chrome Browser Link: reuse an existing same-origin tab without navigating or reloading; open a tab only if no match exists.

This is a beta. Protocol encoding and UI tests are automated. Onboard write persistence and a real Chrome-extension round trip still require hardware/browser validation; this release does not claim universal support for visually similar mini keyboards.

