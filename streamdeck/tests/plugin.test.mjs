import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawn } from 'node:child_process';
import { WebSocketServer } from 'ws';
import { request, connection } from '../src/bridge.mjs';

test('packaged plugin registers and controls a local app through real SDK events', { timeout: 20000 }, async () => {
  const fixture = await mkdtemp(join(tmpdir(), 'openmic-streamdeck-'));
  const config = join(fixture, 'OpenMicAI', 'streamdeck.json');
  await mkdir(join(fixture, 'OpenMicAI'));
  const token = 'a'.repeat(64), calls = [], messages = [];
  let phase = 'idle', child, socket;
  const http = createServer(async (req, res) => {
    assert.equal(req.headers.authorization, `Bearer ${token}`);
    if (req.method === 'POST') {
      let raw = ''; for await (const part of req) raw += part;
      const { command } = JSON.parse(raw); calls.push(command);
      if (['toggle', 'hold.down'].includes(command)) phase = phase === 'recording' ? 'idle' : 'recording';
      if (['hold.up', 'cancel'].includes(command)) phase = 'idle';
    }
    res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify({ ok: true, ready: true, phase, hasRecent: true }));
  });
  await new Promise(resolve => http.listen(0, '127.0.0.1', resolve));
  const ws = new WebSocketServer({ host: '127.0.0.1', port: 0 });
  await new Promise(resolve => ws.once('listening', resolve));
  const info = { application: { version: '7.5.0', platform: process.platform === 'win32' ? 'windows' : 'mac', platformVersion: '15', language: 'en', fontFamily: 'Arial' }, plugin: { uuid: 'com.openmicai.controls', version: '1.0.0.0' }, devices: [{ id: 'deck', name: 'Test', type: 0, size: { columns: 5, rows: 3 } }], colors: {}, devicePixelRatio: 1 };
  const waitFor = async predicate => { const deadline = Date.now() + 5000; while (!predicate()) { if (Date.now() > deadline) throw new Error(`Timed out. Child exited: ${child?.exitCode}. Messages: ${JSON.stringify(messages.slice(-4))}`); await new Promise(resolve => setTimeout(resolve, 20)); } };
  try {
    await writeFile(config, JSON.stringify({ port: http.address().port, token, application: process.execPath }));
    assert.equal((await request('status', { path: config })).ready, true);
    await writeFile(config, JSON.stringify({ port: 70000, token, application: process.execPath }));
    await assert.rejects(connection(config), /Invalid local connection/);
    await writeFile(config, JSON.stringify({ port: http.address().port, token, application: process.execPath }));
    ws.on('connection', client => { socket = client; client.on('message', raw => messages.push(JSON.parse(raw))); });
    child = spawn(process.execPath, ['bin/plugin.mjs', '-port', String(ws.address().port), '-pluginUUID', 'test-plugin', '-registerEvent', 'registerPlugin', '-info', JSON.stringify(info)], { cwd: resolve('com.openmicai.controls.sdPlugin'), env: { ...process.env, LOCALAPPDATA: fixture, ...(process.platform === 'win32' ? {} : { HOME: fixture }) }, stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
    // macOS discovers the config in Application Support instead of LOCALAPPDATA.
    if (process.platform !== 'win32') { const dir = join(fixture, 'Library', 'Application Support', 'Open Mic AI'); await mkdir(dir, { recursive: true }); await writeFile(join(dir, 'streamdeck.json'), JSON.stringify({ port: http.address().port, token, application: '/Applications/Open Mic AI.app' })); }
    let stderr = ''; child.stderr.on('data', data => { stderr += data; });
    await waitFor(() => messages.some(x => x.event === 'registerPlugin'));
    const send = (event, kind) => socket.send(JSON.stringify({ event, action: `com.openmicai.controls.${kind}`, context: kind, device: 'deck', payload: { settings: {}, controller: 'Keypad', state: 0, isInMultiAction: false, coordinates: { column: 0, row: 0 } } }));
    for (const kind of ['dictate', 'hold', 'copy', 'open', 'cancel']) send('willAppear', kind);
    await waitFor(() => messages.some(x => x.event === 'setTitle' && x.payload.title === 'Dictate'));
    send('keyDown', 'dictate'); await waitFor(() => calls.includes('toggle'));
    await waitFor(() => messages.some(x => x.event === 'setTitle' && x.payload.title === 'Stop'));
    send('keyDown', 'dictate'); await waitFor(() => calls.filter(x => x === 'toggle').length === 2);
    send('keyDown', 'hold'); send('keyUp', 'hold');
    await waitFor(() => calls.includes('hold.down') && calls.includes('hold.up') && phase === 'idle');
    for (const kind of ['copy', 'open', 'cancel']) { send('keyDown', kind); await waitFor(() => calls.includes(kind)); }
    assert.ok(messages.some(x => x.event === 'showOk' && x.context === 'copy'));
    assert.equal(stderr, '');
    assert.ok(!messages.some(x => x.event === 'showAlert'));
    assert.ok(messages.some(x => x.event === 'setImage' && x.payload.image === 'images/plugin.png'));
  } finally {
    if (child && child.exitCode === null) { const exited = new Promise(resolve => child.once('exit', resolve)); child.kill(); await exited; }
    for (const client of ws.clients) client.terminate(); ws.close(); http.closeAllConnections(); await new Promise(resolve => http.close(resolve));
    assert.ok(fixture.startsWith(join(tmpdir(), 'openmic-streamdeck-'))); await rm(fixture, { recursive: true, force: true });
  }
});
