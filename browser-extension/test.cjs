const assert = require('node:assert/strict');
const { siteKey, focusOrOpen, navigateResult, handleRequest, translationService, translateResult, fillTranslationInput } = require('./background.js');
function fake(tabs, state='normal') {
  const calls=[];
  return {calls, tabs:{query:async()=>tabs,update:async(...x)=>calls.push(['tab',...x]),create:async(x)=>{calls.push(['create',x]);return {id:88,windowId:5};}}, windows:{get:async()=>({state}),update:async(...x)=>calls.push(['window',...x])}};
}
(async()=>{
 assert.equal(siteKey('https://app.hyperliquid.xyz/trade/BTC?x=1'),'https://app.hyperliquid.xyz');
 assert.throws(()=>siteKey('file:///secret'));
 let api=fake([{id:1,windowId:7,url:'https://kr.tradingview.com/chart/saved',lastAccessed:9}],'minimized');
 assert.equal((await focusOrOpen(api,'https://kr.tradingview.com/')).action,'focused');
 assert.deepEqual(api.calls,[['tab',1,{active:true}],['window',7,{state:'normal',focused:true}]]);
 api=fake([{id:2,windowId:8,url:'https://app.hyperliquid.xyz/trade/ETH',lastAccessed:4},{id:3,windowId:9,url:'https://app.hyperliquid.xyz/portfolio',lastAccessed:10}]);
 await focusOrOpen(api,'https://app.hyperliquid.xyz/trade');assert.equal(api.calls[0][1],3);
 api=fake([{id:1,windowId:1,url:'https://app.hyperliquid.xyz.evil.example/trade'}]);
 assert.equal((await focusOrOpen(api,'https://app.hyperliquid.xyz/trade')).action,'opened');
 api=fake([{id:1,windowId:1,url:'https://app.hyperliquid.xyz/trade',incognito:true}]);
 assert.equal((await focusOrOpen(api,'https://app.hyperliquid.xyz/trade')).action,'opened');
 api=fake([{id:1,windowId:1,pendingUrl:'https://app.hyperliquid.xyz/trade'}]);
 assert.equal((await focusOrOpen(api,'https://app.hyperliquid.xyz/trade')).action,'focused');
 api=fake([{id:1,windowId:1,url:'https://app.hyperliquid.xyz/trade'}]);
 api.tabs.update=async()=>{throw new Error('closed')};
 await assert.rejects(()=>focusOrOpen(api,'https://app.hyperliquid.xyz/trade'));
 assert.equal(api.calls.length,0); // No duplicate-opening fallback after a failed activation.
 const saved={};api=fake([]);api.storage={session:{get:async(key)=>({[key]:saved[key]}),set:async(value)=>Object.assign(saved,value)}};
 api.tabs.get=async(id)=>({id,windowId:5,url:'https://www.google.com/search?q=old'});
 assert.deepEqual(await handleRequest(api,{operation:'capabilities'}),{ok:true,navigate:true,translate:true});assert.equal(api.calls.length,0);
 await navigateResult(api,'https://www.google.com/search?q=first','google');
 assert.equal(api.calls.filter(c=>c[0]==='create').length,1);
 await navigateResult(api,'https://www.google.com/search?q=second','google');
 assert.deepEqual(api.calls.find(c=>c[0]==='tab'),['tab',88,{url:'https://www.google.com/search?q=second',active:true}]);
 assert.equal(api.calls.filter(c=>c[0]==='create').length,1);
 assert.deepEqual(saved['result:google'],{tabId:88,origin:'https://www.google.com'}); // No selected text stored.
 api.tabs.get=async()=>({id:88,windowId:5,url:'chrome://newtab/'});
 await navigateResult(api,'https://www.google.com/search?q=third','google');
 assert.equal(api.calls.filter(c=>c[0]==='create').length,2);
 api.tabs.get=async()=>{throw Error('closed')};await navigateResult(api,'https://www.google.com/search?q=fourth','google');
 assert.equal(api.calls.filter(c=>c[0]==='create').length,3);
 await assert.rejects(()=>handleRequest(api,{operation:'unknown'}));
 await assert.rejects(()=>navigateResult(api,'file:///x','google'));
 await assert.rejects(()=>navigateResult(api,'https://example.com','bad engine'));
 const longText='한글 sample & ? #\n'.repeat(240); // More than 2,000 UTF-16 characters.
 assert.ok(longText.length>2000 && longText.length<5000);
 assert.equal(translationService('https://translate.google.com/?sl=auto&tl=ko&op=translate',longText).limit,5000);
 for(const invalid of ['https://papago.naver.com.evil.example/','http://papago.naver.com/','https://papago.naver.com/docs','https://papago.naver.com/?text=secret','https://user@papago.naver.com/','https://papago.naver.com:444/'])assert.throws(()=>translationService(invalid,'sample'));
 assert.throws(()=>translationService('https://papago.naver.com/', 'a'.repeat(3001)),e=>e.code==='translation_length');
 const tracked={}; api=fake([]);let current;
 api.storage={session:{get:async key=>({[key]:tracked[key]}),set:async v=>Object.assign(tracked,v)}};
 api.tabs.create=async props=>{api.calls.push(['create',props]);current={id:88,windowId:5,url:props.url,status:'complete'};return current;};
 api.tabs.get=async()=>current;
 api.scripting={executeScript:async options=>{api.calls.push(['script',options]);return [{result:{ok:true}}];}};
 assert.equal((await translateResult(api,'https://translate.google.com/?sl=auto&tl=ko&op=translate','translate',longText)).action,'translated');
 assert.equal(api.calls.find(c=>c[0]==='script')[1].args[1],longText);
 assert.equal(api.calls.find(c=>c[0]==='script')[1].func,fillTranslationInput);
 assert.ok(!JSON.stringify(tracked).includes('sample'));
 assert.ok(!api.calls.find(c=>c[0]==='create')[1].url.includes('text='));
 const beforeCalls=api.calls.length;await assert.rejects(()=>translateResult(api,'https://papago.naver.com/','papago','a'.repeat(3001)));
 assert.equal(api.calls.length,beforeCalls); // Reject before opening a tab or sending text.
 current={...current,url:'https://translate.google.com/'};
 api.tabs.get=async()=>({...current,pendingUrl:'https://evil.example/'});
 await assert.rejects(()=>translateResult(api,'https://translate.google.com/','translate',longText));
 assert.equal(api.calls.filter(c=>c[0]==='script').length,1); // Navigation race never receives text.
 console.log('PASS: long Unicode translation uses raw editor input, approved origins only, limits reject before navigation, no text storage, navigation race rejects before injection');
 console.log('PASS: capability probe has no side effects, confirmed searches navigate only owned result tabs, repurposed/closed tabs are safe, text is not stored');
 console.log('PASS: existing routes preserved, minimize restore, MRU selection, absent-tab creation, exact origin isolation, incognito exclusion, loading tabs, failure without duplicate');
})().catch(e=>{console.error(e);process.exitCode=1});
