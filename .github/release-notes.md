MiniDeck 0.2.0-beta.1 adds three modes to the circular dial.

- **Key 1 — Saved apps:** open registered apps, folders and websites.
- **Key 2 — Open windows:** switch between existing windows without starting another app instance.
- **Key 3 — Text actions:** select text, choose a search/translation engine, then press the knob to open the result. Cancelling sends nothing.
- Google, Naver, Bing and Google Translate SVG logos are bundled for offline display in the dial and engine settings.

**1번 등록한 앱 / 2번 열린 창 / 3번 선택한 글 검색·번역**으로 구성했습니다. 기존 여섯 단축키 매핑은 그대로 사용합니다. Google·네이버·Bing 검색과 Google 번역을 제공하며 설정에서 엔진 주소를 편집할 수 있습니다.

### Upgrade / 업데이트

Replace both executables and the extension folder, then reload **MiniDeck Browser Link** at chrome://extensions. Version 1.1.0 adds session storage for engine-specific result-tab IDs and origins; selected text is not stored there. Search actions update their dedicated result tab; ordinary website entries keep their current page.

실행 파일 두 개와 확장 폴더를 교체한 뒤 Chrome에서 MiniDeck 확장을 새로고침하세요. 앱 목록·아이콘·다이얼 위치와 크기는 유지됩니다. 기존 키별 직접 실행 설정은 세 모드로 대체됩니다.

### Downloads / 다운로드

- English: MiniDeck-0.2.0-beta.1-win-en.zip
- 한국어: MiniDeck-0.2.0-beta.1-win-ko.zip
- SHA256SUMS.txt

Extract the entire ZIP and run **MiniDeck.exe**. Windows 10/11 and .NET Framework 4.8+ are required. Keep the DLLs and fonts together; run one language edition at a time.

### Validation

Both language builds passed 15 self-test groups. Checks cover legacy settings, text encoding, selection from a real Windows text control, explicit confirmation/cancellation, closed-window rejection, existing dial behavior, browser routing and isolated native messaging. Both language previews were visually checked.

The connected-device text-selection → key 3 → engine → knob press → result-page flow was confirmed by the user. Existing website-tab activation and Chrome foreground behavior were confirmed earlier. Broader app compatibility, elevated windows, mixed DPI and onboard write persistence remain unverified. Builds are unsigned and global shortcuts respond to any keyboard.

Personal settings, manufacturer software and hardware investigation files are excluded.
