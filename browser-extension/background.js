/* Only approved URLs and tab/window identifiers are used. Confirmed text actions
   navigate to the selected service with the query; no page scraping is used. */
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
async function navigateResult(api, url, engine) {
  const origin = siteKey(url);
  if (typeof engine !== 'string' || !/^[a-zA-Z0-9_-]{1,80}$/.test(engine)) throw new Error('Invalid engine id');
  const key = 'result:' + engine;
  const saved = (await api.storage.session.get(key))[key];
  let tab;
  if (saved) {
    try { tab = await api.tabs.get(saved.tabId); } catch { /* Closed tab. */ }
    // A user may have repurposed our result tab. Never overwrite that tab.
    try { if (tab && (tab.incognito || siteKey(tab.pendingUrl || tab.url) !== origin || saved.origin !== origin)) tab = null; }
    catch { tab = null; }
  }
  if (tab) await api.tabs.update(tab.id, { url, active: true });
  else tab = await api.tabs.create({ url, active: true });
  await api.storage.session.set({ [key]: { tabId: tab.id, origin } });
  const window = await api.windows.get(tab.windowId);
  await api.windows.update(tab.windowId, window.state === 'minimized' ? { state: 'normal', focused: true } : { focused: true });
  return { ok: true, action: 'navigated', tabId: tab.id };
}
async function handleRequest(api, request) {
  if (request.operation === 'capabilities') return { ok: true, navigate: true };
  if (request.operation === 'navigate') return navigateResult(api, request.url, request.engine);
  if (request.operation && request.operation !== 'open') throw new Error('Unknown operation');
  return focusOrOpen(api, request.url);
}
if (typeof module !== 'undefined') module.exports = { siteKey, focusOrOpen, navigateResult, handleRequest };
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
        try { response = await handleRequest(chrome, request); }
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
