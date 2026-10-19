import { readFile } from 'node:fs/promises';
import { homedir } from 'node:os';
import { join, isAbsolute } from 'node:path';
import { spawn } from 'node:child_process';

export function connectionPath(platform = process.platform, home = homedir(), local = process.env.LOCALAPPDATA) {
  return platform === 'win32' ? join(local || join(home, 'AppData', 'Local'), 'OpenMicAI', 'streamdeck.json')
    : join(home, 'Library', 'Application Support', 'Open Mic AI', 'streamdeck.json');
}
export async function connection(path = connectionPath()) {
  const value = JSON.parse(await readFile(path, 'utf8'));
  if (!Number.isInteger(value.port) || value.port < 0 || value.port > 65535 || !/^[a-f0-9]{64}$/.test(value.token)) throw new Error('Invalid local connection');
  return value;
}
export async function request(command = 'status', options = {}) {
  const value = await connection(options.path);
  if (!value.port) throw new Error('Open Mic AI is closed');
  const response = await fetch(`http://127.0.0.1:${value.port}/${command === 'status' ? 'status' : 'command'}`, {
    method: command === 'status' ? 'GET' : 'POST',
    headers: { Authorization: `Bearer ${value.token}`, 'Content-Type': 'application/json', 'X-OpenMic-Client': 'StreamDeck' },
    ...(command === 'status' ? {} : { body: JSON.stringify({ command }) }),
    signal: AbortSignal.timeout(options.timeout || 1200), redirect: 'error'
  });
  if (!response.ok) throw new Error('Open Mic AI could not accept the command');
  const state = await response.json();
  if (state.ok === false) throw new Error(state.error || 'Open Mic AI is busy');
  return state;
}
export async function launch(open = false) {
  const value = await connection();
  const path = value.application;
  if (typeof path !== 'string' || !isAbsolute(path)) throw new Error('Open Open Mic AI first');
  let child;
  if (process.platform === 'win32' && /\.exe$/i.test(path)) child = spawn(path, [open ? '--open' : '--background'], { detached: true, stdio: 'ignore', windowsHide: true });
  else if (process.platform === 'darwin' && path.endsWith('.app')) child = spawn('/usr/bin/open', [...(open ? [] : ['-g']), '-a', path, '--args', ...(open ? [] : ['--background'])], { detached: true, stdio: 'ignore' });
  else throw new Error('Open Open Mic AI first');
  await new Promise((resolve, reject) => { child.once('spawn', resolve); child.once('error', reject); });
  child.unref();
}
