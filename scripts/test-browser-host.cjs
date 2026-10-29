const assert = require('node:assert/strict');
const net = require('node:net');
const {spawn} = require('node:child_process');
const path = require('node:path');
const hostPath = path.resolve(process.argv[2] || 'MiniDeck.BrowserHost.exe');
const sid = process.argv[3];
if (!sid || !/^S-1-/.test(sid)) throw Error('Pass the current Windows user SID as the second argument.');
const suffix = require('node:crypto').randomUUID();
const host = spawn(hostPath, ['--test-pipe',suffix], {windowsHide:true, stdio:['pipe','pipe','pipe']});
let nativeBytes = Buffer.alloc(0);
let forwarded=0;
host.stdout.on('data', data => {
 nativeBytes = Buffer.concat([nativeBytes,data]);
 while(nativeBytes.length >= 4 && nativeBytes.length >= 4 + nativeBytes.readUInt32LE(0)) {
  const length=nativeBytes.readUInt32LE(0);
  const request=JSON.parse(nativeBytes.subarray(4,length+4));
  forwarded++;
  nativeBytes=nativeBytes.subarray(length+4);
  assert.ok(['capabilities','navigate','open','translate'].includes(request.operation));
  if(request.operation==='translate'){assert.equal(request.url,'https://translate.google.com/?sl=auto&tl=ko&op=translate');assert.equal(request.text,'한글'.repeat(2400));}
  else if(request.operation!=='capabilities')assert.equal(request.url,'https://example.com/test');
  const reply=Buffer.from(JSON.stringify({id:request.id,ok:true,action:'focused',navigate:true,translate:true}));
  const prefix=Buffer.alloc(4);prefix.writeUInt32LE(reply.length);
  host.stdin.write(Buffer.concat([prefix,reply]));
 }
});
async function connect() {
 for(let i=0;i<50;i++) {
  try {return await new Promise((resolve,reject)=>{const socket=net.connect('\\\\.\\pipe\\MiniDeck.Browser.'+sid+'.test.'+suffix);socket.once('connect',()=>resolve(socket));socket.once('error',reject);});}
  catch {await new Promise(r=>setTimeout(r,100));}
 }
 throw Error('Native host pipe did not become available');
}
const deadline=setTimeout(()=>{host.kill();process.exit(1);},10000);
(async()=>{
 for(const operation of ['open','capabilities','navigate','translate']){
 const pipe=await connect();
 const result=new Promise((resolve,reject)=>{let text='';pipe.on('data',chunk=>{text+=chunk;if(text.includes('\n'))resolve(JSON.parse(text));});pipe.on('error',reject);});
 pipe.write(JSON.stringify({id:'roundtrip-test',operation,url:operation==='translate'?'https://translate.google.com/?sl=auto&tl=ko&op=translate':operation==='capabilities'?undefined:'https://example.com/test',engine:operation==='navigate'?'google':'translate',text:operation==='translate'?'한글'.repeat(2400):undefined})+'\n');
 assert.deepEqual(await result,{id:'roundtrip-test',ok:true,action:'focused',navigate:true,translate:true});
 pipe.destroy();
 }
 for(const [url,text] of [['https://translate.google.com/','x'.repeat(5001)],['https://papago.naver.com/','x'.repeat(3001)],['https://papago.naver.com.evil.example/','sample']]){
  const before=forwarded;const pipe=await connect();
  const result=new Promise((resolve,reject)=>{let text='';pipe.on('data',chunk=>{text+=chunk;if(text.includes('\n'))resolve(JSON.parse(text));});pipe.on('error',reject);});
  pipe.write(JSON.stringify({id:'reject-test',operation:'translate',url,engine:'translate',text})+'\n');
  assert.equal((await result).ok,false);assert.equal(forwarded,before);pipe.destroy();
 }
 console.log('PASS: local named pipe -> framed native message -> correlated response round trip');
 console.log('PASS: translation limits and unapproved hosts are rejected before forwarding text');
})().catch(error=>{console.error(error);process.exitCode=1;}).finally(()=>{clearTimeout(deadline);host.kill();});
