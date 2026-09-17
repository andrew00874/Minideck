const assert = require('node:assert/strict');
const { siteKey, focusOrOpen } = require('./background.js');
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
 console.log('PASS: existing routes preserved, minimize restore, MRU selection, absent-tab creation, exact origin isolation, incognito exclusion, loading tabs, failure without duplicate');
})().catch(e=>{console.error(e);process.exitCode=1});
