const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const files = ['MiniDeck.cs','Appearance.cs','Onboard.cs','BrowserLink.cs','BrowserHost.cs','DialModes.cs'];
// Consume comments and character literals too, so their quotation marks cannot
// accidentally hide later C# strings from the translation coverage check.
const tokens = /\/\/[^\r\n]*|\/\*[\s\S]*?\*\/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'/g;
const strings = new Set();
for (const file of files) {
 const source = fs.readFileSync(path.join(root,file),'utf8');
 for (const m of source.matchAll(tokens)) {
  if (!m[0].startsWith('"') || !/[가-힣]/.test(m[0])) continue;
  const value = JSON.parse(m[0]);
  if (value === '[가-힣ㄱ-ㅎㅏ-ㅣ]' || value.startsWith('Very Long Application Name')) continue;
  strings.add(value);
 }
}
if (process.argv.includes('--list')) {console.log(JSON.stringify([...strings],null,2));process.exit();}
const map = JSON.parse(fs.readFileSync(path.join(root,'locales/en.json'),'utf8'));
const missing = [...strings].filter(s=>!map[s]);
if(missing.length) throw Error('Untranslated strings: '+JSON.stringify(missing));
if(process.argv.includes('--apply')) for(const file of files) {
 let source = fs.readFileSync(path.join(root,file),'utf8');
 source = source.replace(tokens,(token,offset)=>{
  if (!token.startsWith('"') || !/[가-힣]/.test(token)) return token;
  const value=JSON.parse(token);
  if (!map[value] || source.slice(Math.max(0,offset-7),offset)==='L10n.T(') return token;
  return 'L10n.T('+token+')';
 });
 fs.writeFileSync(path.join(root,file),source);
}
console.log('PASS: English translations cover '+strings.size+' user-facing Korean strings.');
