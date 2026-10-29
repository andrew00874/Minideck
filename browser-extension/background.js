/* Text is sent only after dial confirmation. Translation uses the source editor
   on two approved origins; selected text is never written to extension storage. */
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
function translationService(url, text) {
  const u = new URL(url);
  if (u.protocol !== 'https:' || u.port || u.username || u.password || u.pathname !== '/' || u.hash ||
      !['translate.google.com', 'papago.naver.com'].includes(u.hostname)) throw new Error('Invalid translation origin');
  for (const [key, value] of u.searchParams) {
    if (!['sl','tl','op'].includes(key) || (key === 'op' ? value !== 'translate' : !/^[a-zA-Z-]{2,20}$/.test(value))) throw new Error('Invalid translation URL');
  }
  const limit = u.hostname === 'papago.naver.com' ? 3000 : 5000;
  if (typeof text !== 'string' || !text.trim() || text.length > limit) {
    const error = new Error('Translation input limit exceeded'); error.code = 'translation_length'; throw error;
  }
  return { origin:u.origin, limit };
}
// This function is serialized by Chrome. It must not capture background variables.
async function fillTranslationInput(origin, text, limit) {
  const failed = code => ({ ok:false, code });
  const onPage = () => location.origin === origin && location.pathname === '/';
  const visible = e => !e.disabled && !e.readOnly && e.getAttribute('aria-readonly') !== 'true' &&
    !e.closest('[inert], [aria-hidden="true"]') && e.getClientRects().length > 0 && getComputedStyle(e).visibility !== 'hidden';
  const deadline = Date.now() + 8000;
  let editor;
  while (Date.now() < deadline) {
    if (!onPage()) return failed('translation_input');
    const candidates = origin === 'https://papago.naver.com'
      ? [...document.querySelectorAll('[data-testid="source-editor"][contenteditable="true"]')].filter(visible)
      : [...document.querySelectorAll('textarea')].filter(visible);
    if (candidates.length === 1) { editor = candidates[0]; break; }
    await new Promise(resolve => setTimeout(resolve,100));
  }
  if (!editor || !onPage()) return failed('translation_input');
  const maximum = editor.maxLength > 0 ? Math.min(limit, editor.maxLength) : limit;
  if (text.length > maximum) return failed('translation_length');
  const normalized = text.replace(/\r\n?/g,'\n');
  editor.focus();
  if (document.activeElement !== editor) return failed('translation_input');
  if (editor.tagName === 'TEXTAREA') {
    const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set;
    setter.call(editor,normalized);
    editor.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:normalized}));
    editor.dispatchEvent(new Event('change',{bubbles:true}));
  } else {
    // Papago's Lexical editor handles plain-text paste and preserves line breaks.
    // insertText treats embedded newlines as ordinary text and loses them there.
    const range = document.createRange(); range.selectNodeContents(editor);
    const selection = window.getSelection(); selection.removeAllRanges(); selection.addRange(range);
    const clipboardData = new DataTransfer(); clipboardData.setData('text/plain',normalized);
    editor.dispatchEvent(new ClipboardEvent('paste',{bubbles:true,cancelable:true,clipboardData}));
  }
  await new Promise(resolve => setTimeout(resolve,400));
  if (!onPage() || !editor.isConnected) return failed('translation_input');
  // innerText puts two newlines between block paragraphs. Papago sends one
  // separator per paragraph, and an empty <p><br></p> represents an empty line.
  const actual = editor.tagName === 'TEXTAREA' ? editor.value : [...editor.children].map(paragraph =>
    paragraph.childNodes.length === 1 && paragraph.firstChild.nodeName === 'BR' ? '' : paragraph.innerText
  ).join('\n');
  if (actual.replace(/\r\n?/g,'\n') !== normalized) return failed('translation_input');
  return { ok:true };
}
async function translateResult(api, url, engine, text) {
  const service = translationService(url,text);
  const result = await navigateResult(api,url,engine);
  const deadline = Date.now()+10000;
  let ready = false;
  while (Date.now()<deadline) {
    const tab = await api.tabs.get(result.tabId);
    const current = new URL(tab.pendingUrl || tab.url);
    if (tab.incognito || current.origin !== service.origin || current.pathname !== '/') throw new Error('Translation page changed');
    if (tab.status === 'complete' && !tab.pendingUrl) { ready=true; break; }
    await new Promise(resolve=>setTimeout(resolve,100));
  }
  if (!ready) throw new Error('Translation page load timed out');
  const injected = await api.scripting.executeScript({
    target:{tabId:result.tabId}, func:fillTranslationInput, args:[service.origin,text,service.limit]
  });
  const filled = injected[0] && injected[0].result;
  if (!filled || !filled.ok) { const error = new Error('Translation input failed');error.code=filled?.code || 'translation_input';throw error; }
  return {...result,action:'translated'};
}
async function handleRequest(api, request) {
  if (request.operation === 'capabilities') return { ok: true, navigate: true, translate:true };
  if (request.operation === 'navigate') return navigateResult(api, request.url, request.engine);
  if (request.operation === 'translate') return translateResult(api,request.url,request.engine,request.text);
  if (request.operation && request.operation !== 'open') throw new Error('Unknown operation');
  return focusOrOpen(api, request.url);
}
if (typeof module !== 'undefined') module.exports = { siteKey, focusOrOpen, navigateResult, handleRequest, translationService, fillTranslationInput, translateResult };
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
        catch (error) { response = request.operation === 'translate'
          ? {ok:false,code:error.code || 'translation_input',error:'Translation input failed'}
          : { ok: false, error: String(error.message || error) }; }
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
