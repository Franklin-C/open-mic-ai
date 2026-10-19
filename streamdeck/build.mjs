import { build } from 'esbuild';
import { mkdir, writeFile, copyFile, readFile } from 'node:fs/promises';
const root = 'com.openmicai.controls.sdPlugin';
await mkdir(`${root}/bin`, { recursive: true });
await mkdir(`${root}/images`, { recursive: true });
await writeFile(`${root}/package.json`, JSON.stringify({ type: 'module' }));
await build({ entryPoints: ['src/plugin.mjs'], outfile: `${root}/bin/plugin.mjs`, bundle: true, platform: 'node', target: 'node24', format: 'esm', banner: { js: "import { createRequire as __createRequire } from 'node:module';const require=__createRequire(import.meta.url);" } });
const icons = {
  mic: '<rect x="55" y="24" width="34" height="58" rx="17"/><path d="M43 67v7a29 29 0 0 0 58 0v-7M72 104v15M56 119h32"/>',
  recording: '<circle cx="72" cy="64" r="26" fill="#e0303e" stroke="none"/><path d="M45 108h54"/>',
  working: '<path d="M100 42a38 38 0 1 0 5 41"/><circle cx="107" cy="63" r="5" fill="#e0303e" stroke="none"/>',
  copy: '<path d="M51 91H38V31h53v13"/><rect x="53" y="48" width="53" height="65" rx="5"/>',
  cancel: '<path d="m47 42 50 50m0-50L47 92"/>'
};
for (const [name, body] of Object.entries(icons)) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><rect width="144" height="144" rx="20" fill="#fff"/><g fill="none" stroke="#e0303e" stroke-width="7" stroke-linecap="round" stroke-linejoin="round">${body}</g></svg>`;
  await writeFile(`${root}/images/${name}.svg`, svg);
}
await copyFile('../windows/assets/open-mic-ai-ui.png', `${root}/images/plugin.png`);
await copyFile('../windows/assets/open-mic-ai-ui.png', `${root}/images/plugin@2x.png`);
// PNG assets use their normal extension when setImage is called at runtime.
await copyFile('../LICENSE', `${root}/LICENSE.txt`);
let notices = '';
for (const dependency of ['@elgato/streamdeck', '@elgato/utils', '@elgato/schemas', 'ws']) {
  notices += `${dependency}\n\n${await readFile(`node_modules/${dependency}/LICENSE`, 'utf8')}\n\n`;
}
await writeFile(`${root}/THIRD_PARTY_NOTICES.txt`, notices);
