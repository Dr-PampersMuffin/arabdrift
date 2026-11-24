"""ElevenLabs sound effects for the game. Usage: sfx.py [name ...]  (all when no names). Writes Resources/DriftKuwait/Audio/<name>.mp3"""
import os, sys, json, requests, datetime
OUT = os.path.join(os.path.dirname(__file__), "../../client/Assets/DriftKuwait/Resources/DriftKuwait/Audio")
FX = {
    # Street engine matched to a real Kuwaiti street-stunt recording: a screaming high-revving straight-six, raw straight-through exhaust
    "street6_idle": ("Steady idle loop of a tuned 1980s straight-six car engine with a lumpy race camshaft and a loud straight-through exhaust, deep burble, no revving, no tyres, no music, close microphone", 4.0),
    "street6_2500": ("Steady constant loop of a tuned 1980s straight-six engine held at 2500 rpm under load, raspy loud straight-through exhaust, constant pitch, no gear changes, no tyres, no music", 4.0),
    "street6_4500": ("Steady constant loop of a tuned 1980s straight-six engine held at 4500 rpm full throttle, aggressive raspy exhaust howl, constant pitch, no gear changes, no tyres, no music", 4.0),
    "street6_6500": ("Steady constant loop of a tuned 1980s straight-six engine screaming at 6500 rpm full throttle, high-pitched metallic howl from a straight-through exhaust, constant pitch, no gear changes, no tyres, no music", 4.0),
    "street6_8000": ("Steady constant loop of a tuned 1980s straight-six race engine screaming at 8000 rpm at the redline, piercing high-pitched wail, constant pitch, no gear changes, no tyres, no music", 4.0),
    "street6_rev": ("A tuned straight-six street car revving hard in neutral, sharp throttle blips from idle to 8000 rpm and back with loud exhaust crackles and pops between blips, Kuwaiti street meet", 4.0),
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
