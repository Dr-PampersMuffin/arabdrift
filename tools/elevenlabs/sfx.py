"""ElevenLabs sound effects for the game. Usage: sfx.py [name ...]  (all when no names). Writes Resources/DriftKuwait/Audio/<name>.mp3"""
import os, sys, json, requests, datetime
OUT = os.path.join(os.path.dirname(__file__), "../../client/Assets/DriftKuwait/Resources/DriftKuwait/Audio")
FX = {
    "exhaust_pops": ("Aggressive 1980s turbo sports car exhaust backfire on lift-off: a rapid burst of loud gunshot-like pops, bangs and crackles from the exhaust, dry, close microphone, no music", 2.0),
    "rev_limiter": ("A tuned four-cylinder turbo engine bouncing off the rev limiter at full throttle, rapid stuttering brap-brap-brap with exhaust pops, very loud, close", 2.5),
    "exhaust_scrape": ("A lowered car's steel exhaust pipe dragging and scraping along rough asphalt at speed, harsh grinding metal screech with sparks, continuous loop", 3.0),
    "tyre_squeal_high": ("High-pitched screaming tyre squeal of a car drifting sideways at speed on hot asphalt, sustained continuous loop, no engine", 4.0),
    "tyre_burst_smoke": ("Rear tyres spinning in a burnout on asphalt, roaring rubber friction with a hissing smoky whine, continuous loop", 3.0),
}
def main():
    key = os.environ["ELEVENLABS_API_KEY"]
    names = sys.argv[1:] or list(FX)
    for n in names:
        text, dur = FX[n]
        r = requests.post("https://api.elevenlabs.io/v1/sound-generation", headers={"xi-api-key": key},
                          json={"text": text, "duration_seconds": dur, "prompt_influence": 0.6}, timeout=180)
        r.raise_for_status()
        p = os.path.join(OUT, n + ".mp3")
        open(p, "wb").write(r.content)
        with open(os.path.join(os.path.dirname(__file__), "provenance.jsonl"), "a") as f:
            f.write(json.dumps({"kind": "sfx", "key": n, "text": text, "date": datetime.datetime.now(datetime.timezone.utc).isoformat()}) + "\n")
        print("sfx", n, len(r.content) // 1024, "KB")
if __name__ == "__main__":
    main()
