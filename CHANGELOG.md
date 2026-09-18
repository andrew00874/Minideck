# Changelog

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
