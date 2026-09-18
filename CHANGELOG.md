# Changelog

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
