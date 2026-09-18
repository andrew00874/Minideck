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

Install in one profile only. The extension uses `tabs`, `nativeMessaging`, `alarms`, and `storage`. It does not scrape page contents, cookies or passwords. The native host uses a Windows named pipe restricted to your current user.

Version 1.1.0 adds selected-text actions: after you confirm an engine in MiniDeck, the extension opens its result URL containing the selected text. It updates only that engine's dedicated result tab, leaving ordinary website entries unchanged. Session storage contains tab IDs and origins, not the query or original text. Restarting Chrome or reloading the extension resets that tracking. Confirmed queries can appear in normal browser history and are sent to the selected service.

When upgrading from 1.0.0, update both MiniDeck executables and this folder, then click **Reload** for MiniDeck Browser Link at `chrome://extensions`. A capability probe contains no selected text and refuses to use an outdated host/extension for text actions.

When disconnected, click the extension icon to reconnect or wait up to 30 seconds. MiniDeck will show an error instead of falling back to opening duplicate tabs. If you move the MiniDeck folder, run `setup.ps1` again. Close Chrome before replacing the host executable if it is in use.

To remove the integration, remove the extension from Chrome and delete the registry key `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.minideck.browser`.

Developer check: `node test.cjs`. This checks routing with a fake browser API. A real installed-extension test is still required before claiming end-to-end validation.
