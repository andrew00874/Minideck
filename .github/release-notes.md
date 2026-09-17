MiniDeck is a circular Windows app launcher for a three-key macro pad with a pressable dial.

### Downloads / 다운로드

- **English:** `MiniDeck-0.1.0-beta.1-win-en.zip`
- **한국어:** `MiniDeck-0.1.0-beta.1-win-ko.zip`
- `SHA256SUMS.txt` contains the checksums for both ZIP files.

Extract the entire archive and run **MiniDeck.exe**. Keep the DLLs and `fonts` folder together. Requires Windows 10/11 and .NET Framework 4.8+. Both language editions share settings; run one at a time.

압축 전체를 풀고 **MiniDeck.exe**를 실행하세요. DLL과 `fonts` 폴더를 함께 유지해야 합니다. 한국어판·영문판은 설정을 공유하므로 하나씩 실행하세요.

### Included

- Smooth circular dial with animated selection, saved position and adjustable size.
- Custom PNG/SVG icons, modern bundled fonts and full wrapped app names.
- Three shortcut keys, rotary navigation and tray/startup options.
- Onboard mapping editor for the identified `1189:8890`, Report ID 3 protocol.
- Chrome Browser Link extension for reusing an existing website tab. One-time unpacked extension setup is required; see `browser-extension/README.md`.

### Beta validation limits

Both language builds passed the app self-tests. English UI previews were reviewed. Browser routing and local native-message transport tests passed. A clean source checkout was built successfully.

The actual installed Chrome extension and onboard write persistence still need end-to-end validation. Global shortcuts currently respond to any keyboard. Similar-looking macro pads may use different hardware/protocols. Builds are unsigned.

Personal settings, original manufacturer software and hardware investigation files are excluded.
