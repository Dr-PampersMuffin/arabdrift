// DRIFT KUWAIT sound pack generator (ElevenLabs text-to-sound-effects).
// Usage (Node 18+):
//   ELEVENLABS_API_KEY=xxxx node Tools/elevenlabs/generate-sounds.mjs
//   node Tools/elevenlabs/generate-sounds.mjs --only shaheen_4000 --force
// Writes MP3s into Assets/DriftKuwait/Resources/DriftKuwait/Audio/ and logs every prompt and
// response id into Tools/elevenlabs/audio-provenance.json (keep this file: it is the licence trail).
import { readFile, writeFile, mkdir, access } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(here, "..", "..", "client");   // the Unity project lives in client/
const outDir = join(projectRoot, "Assets", "DriftKuwait", "Resources", "DriftKuwait", "Audio");
const logPath = join(here, "audio-provenance.json");

const key = process.env.ELEVENLABS_API_KEY;
if (!key) { console.error("Set ELEVENLABS_API_KEY first."); process.exit(1); }

const args = process.argv.slice(2);
const force = args.includes("--force");
const onlyIdx = args.indexOf("--only");
const only = onlyIdx >= 0 ? args[onlyIdx + 1] : null;

const { sounds } = JSON.parse(await readFile(join(here, "sounds.json"), "utf8"));
await mkdir(outDir, { recursive: true });
let log = [];
try { log = JSON.parse(await readFile(logPath, "utf8")); } catch { /* first run */ }

const exists = async p => { try { await access(p); return true; } catch { return false; } };

// Loops are requested as raw PCM and wrapped as WAV: MP3 adds encoder padding that clicks at the loop point.
function wavFromPcm(pcm, rate) {
  const h = Buffer.alloc(44);
  h.write("RIFF", 0); h.writeUInt32LE(36 + pcm.length, 4); h.write("WAVE", 8);
  h.write("fmt ", 12); h.writeUInt32LE(16, 16); h.writeUInt16LE(1, 20); h.writeUInt16LE(1, 22);
  h.writeUInt32LE(rate, 24); h.writeUInt32LE(rate * 2, 28); h.writeUInt16LE(2, 32); h.writeUInt16LE(16, 34);
  h.write("data", 36); h.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([h, pcm]);
}

async function generate(s, withLoop, format) {
  const body = {
    text: s.text,
    model_id: "eleven_text_to_sound_v2",
    duration_seconds: s.duration ?? null,
    prompt_influence: s.influence ?? 0.3,
  };
  if (withLoop && s.loop) body.loop = true;
  return fetch(`https://api.elevenlabs.io/v1/sound-generation?output_format=${format}`, {
    method: "POST",
    headers: { "xi-api-key": key, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

for (const s of sounds) {
  if (only && s.key !== only) continue;
  const wavFile = join(outDir, `${s.key}.wav`), mp3File = join(outDir, `${s.key}.mp3`);
  if (!force && (await exists(wavFile) || await exists(mp3File))) { console.log(`skip  ${s.key} (exists)`); continue; }

  process.stdout.write(`make  ${s.key} ... `);
  let format = s.loop ? "pcm_44100" : "mp3_44100_128";
  let res = await generate(s, true, format);
  if (!res.ok && s.loop) {                        // PCM may need a higher plan: fall back to MP3
    format = "mp3_44100_128";
    res = await generate(s, true, format);
    if (res.status === 422) res = await generate(s, false, format);
  }
  if (!res.ok) {
    console.log(`FAILED ${res.status}: ${(await res.text()).slice(0, 300)}`);
    continue;
  }
  let buf = Buffer.from(await res.arrayBuffer());
  const isPcm = format.startsWith("pcm");
  if (isPcm) buf = wavFromPcm(buf, 44100);
  const file = isPcm ? wavFile : mp3File;
  await writeFile(file, buf);
  log.push({
    key: s.key, file: `Assets/DriftKuwait/Resources/DriftKuwait/Audio/${s.key}.${isPcm ? "wav" : "mp3"}`, format,
    prompt: s.text, loop: !!s.loop, duration: s.duration, influence: s.influence,
    model: "eleven_text_to_sound_v2",
    requestId: res.headers.get("request-id") || res.headers.get("x-request-id") || null,
    characterCost: res.headers.get("character-cost"),
    bytes: buf.length, createdAt: new Date().toISOString(),
  });
  await writeFile(logPath, JSON.stringify(log, null, 2));
  console.log(`ok (${Math.round(buf.length / 1024)} KB)`);
}
console.log("\nDone. Unity picks the files up automatically next time you press Play.");
