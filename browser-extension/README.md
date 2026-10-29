# MiniDeck Browser Link

[한국어](README.ko.md)

One-time setup in the Chrome profile where you use your websites:

1. Run `setup.ps1` in this folder using PowerShell. It registers the local messaging host for your Windows user.
2. Open `chrome://extensions` in Chrome.
3. Enable **Developer mode**.
4. Click **Load unpacked** and select this `browser-extension` folder.
5. Choose a website in MiniDeck.

Expected extension ID: `mecekhgbdppcabapefkdljicahcnibcf`.

The extension reuses the most recently used normal tab with the same origin (scheme, hostname and port), without navigating or refreshing it. A minimized window is restored. If no matching tab exists, a new one is opened. Different subdomains remain separate. Incognito tabs and other profiles are excluded.

Install in one profile only. The extension uses `tabs`, `nativeMessaging`, `alarms`, `storage`, and `scripting`. Site access is restricted to `https://translate.google.com/*` and `https://papago.naver.com/*`. After confirmation it writes only the source editor and checks that it received the full text. It does not collect other page contents, cookies or passwords. The native host uses a Windows named pipe restricted to your current user.

Version 1.2.0 adds direct editor input for Google Translate and Papago, with no selection embedded in the navigation request URL. Searches continue to use result URLs. It updates only that engine's dedicated result tab, leaving ordinary website entries unchanged. Google Translate accepts 5,000 characters and the current Papago editor accepts 3,000; over-limit requests are rejected before opening a tab. Session storage contains tab IDs and origins, not the query or original text. Restarting Chrome or reloading the extension resets that tracking. Confirmed text is sent to the chosen service; search URLs and URLs updated by the translation website itself can appear in browser history.

When upgrading to 1.2.0, update both MiniDeck executables and this folder, then click **Reload** for MiniDeck Browser Link at `chrome://extensions`. A capability probe contains no selected text and refuses to use an outdated extension for translation.

When disconnected, click the extension icon to reconnect or wait up to 30 seconds. MiniDeck will show an error instead of falling back to opening duplicate tabs. If you move the MiniDeck folder, run `setup.ps1` again. Close Chrome before replacing the host executable if it is in use.

To remove the integration, remove the extension from Chrome and delete the registry key `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.minideck.browser`.

Developer checks: `node test.cjs` covers routing with a fake browser API, and `scripts/test-browser-host.cjs` checks the isolated native-host protocol. On 2026-10-01, the installed extension on Windows/Chrome was verified with 5,000-character Google Translate and 3,000-character Papago samples, including Unicode, blank lines, a trailing newline, and reuse of the same result tabs. The full editor text matched the sample; translated output appeared on both pages. This does not test a physical keyboard or every capture source application.
