#!/usr/bin/env python3
"""Arab Drift voices and music with ElevenLabs (Pro plan).

  python3 tools/elevenlabs/voice_music.py voices   # all lines in lines.json -> Resources/DriftKuwait/Audio/Voice/*.mp3
  python3 tools/elevenlabs/voice_music.py music    # all tracks in music.json -> Resources/DriftKuwait/Audio/Music/*.mp3
  add --only key1 key2 to remake some, --force to overwrite.

Voices come from the ElevenLabs voice library (Kuwaiti and Gulf speakers); each is added to the account once.
Radio lines get a band-passed, slightly crushed "police radio" treatment with ffmpeg.
Every output is logged to tools/elevenlabs/provenance.jsonl (prompt/text, voice, model, date) per the blueprint.
"""
import argparse
import datetime
import json
import os
import subprocess
import sys

import requests

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "client", "Assets", "DriftKuwait", "Resources", "DriftKuwait", "Audio")
API = "https://api.elevenlabs.io/v1"


def key():
    k = os.environ.get("ELEVENLABS_API_KEY")
    if not k:
        sys.exit("ELEVENLABS_API_KEY is not set")
    return k


def log(entry):
    entry["date"] = datetime.datetime.utcnow().isoformat() + "Z"
    with open(os.path.join(HERE, "provenance.jsonl"), "a") as f:
        f.write(json.dumps(entry, ensure_ascii=False) + "\n")


def ensure_voice(cast):
    """Add a library voice to the account (once) and return its id in the account."""
    mine = requests.get(f"{API}/voices", headers={"xi-api-key": key()}, timeout=60).json().get("voices", [])
    for v in mine:
        if v["name"] == cast["name"]:
            return v["voice_id"]
    r = requests.post(f"{API}/voices/add/{cast['owner']}/{cast['voice_id']}", headers={"xi-api-key": key()},
                      json={"new_name": cast["name"]}, timeout=60)
    if r.status_code != 200:
        sys.exit(f"could not add voice {cast['name']}: {r.status_code} {r.text[:300]}")
    return r.json()["voice_id"]


def radio(path):
    tmp = path + ".tmp.mp3"
    os.replace(path, tmp)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", tmp, "-af",
                    "highpass=f=350,lowpass=f=3200,acompressor=threshold=-20dB:ratio=6,volume=1.6,"
                    "aeval='val(0)*0.92+0.08*sgn(val(0))':c=same", "-ar", "22050", path], check=True)
    os.remove(tmp)


def voices(only, force):
    spec = json.load(open(os.path.join(HERE, "lines.json")))
    ids = {}
    out = os.path.join(OUT, "Voice")
    os.makedirs(out, exist_ok=True)
    for line in spec["lines"]:
        k = line["key"]
        if only and k not in only:
            continue
        path = os.path.join(out, k + ".mp3")
        if os.path.exists(path) and not force:
            continue
        cast = spec["cast"][line["who"]]
        if line["who"] not in ids:
            ids[line["who"]] = ensure_voice(cast)
        body = {"text": line["text"], "model_id": spec.get("model", "eleven_v3"),
                "voice_settings": {"stability": line.get("stability", cast.get("stability", 0.5)),
                                   "similarity_boost": 0.8, "style": cast.get("style", 0.3), "use_speaker_boost": True}}
        r = requests.post(f"{API}/text-to-speech/{ids[line['who']]}?output_format=mp3_44100_128",
                          headers={"xi-api-key": key(), "Content-Type": "application/json"}, json=body, timeout=180)
        if r.status_code != 200:
            print("FAILED", k, r.status_code, r.text[:300])
            continue
        open(path, "wb").write(r.content)
        if line.get("radio"):
            radio(path)
        log({"kind": "voice", "key": k, "who": line["who"], "voice": cast["name"], "text": line["text"], "model": body["model_id"]})
        print("voice", k, len(r.content) // 1024, "KB")


def music(only, force):
    spec = json.load(open(os.path.join(HERE, "music.json")))
    out = os.path.join(OUT, "Music")
    os.makedirs(out, exist_ok=True)
    for t in spec["tracks"]:
        k = t["key"]
        if only and k not in only:
            continue
        path = os.path.join(out, k + ".mp3")
        if os.path.exists(path) and not force:
            continue
        body = {"prompt": t["prompt"], "music_length_ms": int(t["seconds"] * 1000), "model_id": "music_v1",
                "force_instrumental": t.get("instrumental", True)}
        r = requests.post(f"{API}/music?output_format=mp3_44100_192", headers={"xi-api-key": key(), "Content-Type": "application/json"},
                          json=body, timeout=600)
        if r.status_code != 200:
            print("FAILED", k, r.status_code, r.text[:400])
            continue
        open(path, "wb").write(r.content)
        log({"kind": "music", "key": k, "prompt": t["prompt"], "seconds": t["seconds"], "model": "music_v1"})
        print("music", k, len(r.content) // 1024, "KB")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("what", choices=["voices", "music"])
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    (voices if a.what == "voices" else music)(a.only, a.force)
