// node cdp.mjs <file with a JS expression that yields a promise of a string>: runs it in headless Chrome, prints the string.
import { spawn } from 'node:child_process';
import { readFileSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const expr = readFileSync(process.argv[2], 'utf8');
const port = 9300 + Math.floor(Math.random() * 500);
const chrome = spawn('C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  ['--headless=new', '--disable-gpu', `--remote-debugging-port=${port}`, `--user-data-dir=${mkdtempSync(join(tmpdir(), 'cdp'))}`, 'about:blank'],
  { stdio: 'ignore' });
let targets;
for (let i = 0; i < 50 && !targets; i++) {
  await new Promise(r => setTimeout(r, 200));
  try { targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); } catch { }
}
const page = targets.find(t => t.type === 'page');
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise(r => ws.onopen = r);
ws.onmessage = e => {
  const m = JSON.parse(e.data);
  if (m.id !== 1) return;
  const r = m.result?.result;
  process.stdout.write(r?.value ?? ('ERR ' + JSON.stringify(m.result?.exceptionDetails ?? m).slice(0, 400)));
  ws.close(); chrome.kill(); process.exit(0);
};
ws.send(JSON.stringify({ id: 1, method: 'Runtime.evaluate', params: { expression: expr, awaitPromise: true, returnByValue: true } }));
