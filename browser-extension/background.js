/* Only URLs and tab/window identifiers are used; nothing is sent to a server. */
function siteKey(value) {
  const u = new URL(value);
  if (!['https:', 'http:'].includes(u.protocol)) throw new Error('Only HTTP(S) URLs are supported.');
  return u.origin; // Keep scheme, port and subdomain; never use substring matching.
}
async function focusOrOpen(api, url) {
  const key = siteKey(url);
  const tabs = await api.tabs.query({});
  const matches = tabs.filter(t => {
    try { return !t.incognito && siteKey(t.pendingUrl || t.url) === key; } catch { return false; }
  }).sort((a, b) => (b.lastAccessed || 0) - (a.lastAccessed || 0));
  if (matches.length) {
    const tab = matches[0];
    // Do not navigate/reload: preserve charts, forms, scroll and the current route.
    await api.tabs.update(tab.id, { active: true });
    const window = await api.windows.get(tab.windowId);
    await api.windows.update(tab.windowId, window.state === 'minimized' ? { state: 'normal', focused: true } : { focused: true });
    return { ok: true, action: 'focused', tabId: tab.id };
  }
  const tab = await api.tabs.create({ url, active: true });
  const window = await api.windows.get(tab.windowId);
  await api.windows.update(tab.windowId, window.state === 'minimized' ? { state: 'normal', focused: true } : { focused: true });
  return { ok: true, action: 'opened', tabId: tab.id };
}
if (typeof module !== 'undefined') module.exports = { siteKey, focusOrOpen };
if (typeof chrome !== 'undefined' && chrome.runtime) {
  let port;
  let queue = Promise.resolve();
  function connect() {
    if (port) return;
    const connection = chrome.runtime.connectNative('com.minideck.browser');
    port = connection;
    connection.onMessage.addListener(request => {
      queue = queue.then(async () => {
        let response;
        try { response = await focusOrOpen(chrome, request.url); }
        catch (error) { response = { ok: false, error: String(error.message || error) }; }
        try { connection.postMessage({ ...response, id: request.id }); } catch {}
      });
    });
    connection.onDisconnect.addListener(() => {
      void chrome.runtime.lastError;
      if (port === connection) port = null;
    });
  }
  chrome.alarms.create('reconnect', { periodInMinutes: 0.5 });
  chrome.alarms.onAlarm.addListener(connect);
  chrome.runtime.onStartup.addListener(connect);
  chrome.runtime.onInstalled.addListener(connect);
  chrome.action.onClicked.addListener(connect);
  connect();
}
