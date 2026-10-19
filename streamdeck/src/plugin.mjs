import streamDeck, { action, SingletonAction } from '@elgato/streamdeck';
import { request, launch } from './bridge.mjs';

export function keyPresentation(kind, state) {
  if (kind === 'copy') return { title: 'Copy', image: 'images/copy.svg' };
  if (kind === 'open') return { title: 'Open app', image: 'images/app.svg' };
  if (kind === 'cancel') return { title: 'Cancel', image: 'images/cancel.svg' };
  if (!state) return { title: 'Open app', image: 'images/mic.svg' };
  if (state.phase === 'recording') return { title: kind === 'hold' ? 'Release' : 'Stop', image: 'images/recording.svg' };
  if (state.phase === 'processing') return { title: 'Working…', image: 'images/working.svg' };
  if (!state.ready) return { title: 'Models', image: 'images/mic.svg' };
  return { title: kind === 'hold' ? 'Hold' : 'Dictate', image: 'images/mic.svg' };
}
let state, polling = false;
const previous = new Map();
async function paint() {
  for (const key of streamDeck.actions) {
    if (!key.isKey()) continue;
    const kind = key.manifestId.split('.').at(-1), view = keyPresentation(kind, state);
    const signature = `${view.title}|${view.image}`;
    if (previous.get(key.id) === signature) continue;
    previous.set(key.id, signature);
    await Promise.all([key.setTitle(view.title), key.setImage(view.image)]);
  }
}
async function poll() {
  if (polling || !streamDeck.actions.length) return;
  polling = true;
  try { state = await request(); } catch { state = undefined; }
  try { await paint(); } catch { /* A profile may disappear during the update. */ }
  finally { polling = false; }
}
class Control extends SingletonAction {
  constructor(kind) { super(); this.kind = kind; this.held = new Set(); }
  onWillAppear() { void poll(); }
  onWillDisappear(ev) {
    previous.delete(ev.action.id);
    if (this.held.delete(ev.action.id)) void request('hold.up').catch(() => {});
  }
  async onKeyDown(ev) {
    try {
      if (this.kind === 'hold') {
        this.held.add(ev.action.id);
        await request('hold.down');
        // A fast release can arrive before the start acknowledgement.
        if (!this.held.has(ev.action.id)) await request('hold.up');
      } else await request(this.kind === 'dictate' ? 'toggle' : this.kind);
      if (this.kind === 'copy') await ev.action.showOk();
    } catch {
      if (this.kind === 'hold') this.held.delete(ev.action.id);
      if (!state) { try { await launch(this.kind === 'open'); } catch {} }
      await ev.action.showAlert();
    }
    void poll();
  }
  async onKeyUp(ev) {
    if (this.kind === 'hold' && this.held.delete(ev.action.id)) {
      try { await request('hold.up'); } catch { await ev.action.showAlert(); }
      void poll();
    }
  }
}
for (const kind of ['dictate', 'hold', 'copy', 'open', 'cancel']) {
  class RegisteredControl extends Control { constructor() { super(kind); } }
  const DecoratedControl = action({ UUID: `com.openmicai.controls.${kind}` })(RegisteredControl);
  streamDeck.actions.registerAction(new DecoratedControl());
}
streamDeck.connect();
setInterval(() => void poll(), 750).unref();
