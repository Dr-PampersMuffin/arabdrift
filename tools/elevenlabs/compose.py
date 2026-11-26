"""Compose a song with Eleven Music v2.5 from a composition plan (JSON file) or a prompt. Never prints the key.
usage: compose.py plan.json out.mp3        (plan = {"chunks":[...]})  or  compose.py --prompt "text" ms out.mp3 [--instrumental]
"""
import json, os, sys, requests
KEY = os.environ["ELEVENLABS_API_KEY"]
url = "https://api.elevenlabs.io/v1/music?output_format=mp3_48000_192"
if sys.argv[1] == "--prompt":
    body = {"prompt": sys.argv[2], "music_length_ms": int(sys.argv[3]), "model_id": "music_v2_5", "force_instrumental": "--instrumental" in sys.argv}
    out = sys.argv[4]
else:
    body = {"composition_plan": json.load(open(sys.argv[1])), "model_id": "music_v2_5"}
    out = sys.argv[2]
r = requests.post(url, headers={"xi-api-key": KEY, "Content-Type": "application/json"}, data=json.dumps(body), timeout=900)
if r.status_code != 200:
    sys.exit(f"{r.status_code}: {r.text[:600]}")
open(out, "wb").write(r.content)
print(out, len(r.content) // 1024, "KB", "song", r.headers.get("song-id") or r.headers.get("x-song-id") or "")
