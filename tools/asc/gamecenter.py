"""Create the game's Game Center leaderboards in App Store Connect (idempotent).

Runs in CI (.github/workflows/gamecenter.yml) with the App Store Connect API key from repository secrets:
APPSTORE_KEY_ID, APPSTORE_ISSUER_ID, APPSTORE_P8. Prints a short report; never prints the key.
"""
import json, os, sys, time

import jwt
import requests

BUNDLE = "world.embur.arabdrift"
API = "https://api.appstoreconnect.apple.com/v1"
BOARDS = [
    # vendor id, reference name, en name, ar name, sort
    ("q8drift.best_run", "Best event score", "Best Run", "أفضل جولة", "DESC"),
    ("q8drift.best_chain", "Best drift chain", "Longest Drift Chain", "أطول سلسلة تقحيص", "DESC"),
    ("q8drift.total_stars", "Career stars", "Career Stars", "نجوم المسيرة", "DESC"),
]


def token():
    key = os.environ["APPSTORE_P8"].replace("\\n", "\n")
    now = int(time.time())
    return jwt.encode({"iss": os.environ["APPSTORE_ISSUER_ID"].strip(), "iat": now, "exp": now + 1100, "aud": "appstoreconnect-v1"},
                      key, algorithm="ES256", headers={"kid": os.environ["APPSTORE_KEY_ID"].strip(), "typ": "JWT"})


H = {"Authorization": "Bearer " + token(), "Content-Type": "application/json"}


def call(method, path, body=None):
    r = requests.request(method, API + path if path.startswith("/") else path, headers=H, data=json.dumps(body) if body else None, timeout=60)
    if r.status_code >= 300:
        print(f"{method} {path} -> {r.status_code}: {r.text[:600]}")
        return None
    return r.json() if r.text else {}


app = call("GET", f"/apps?filter[bundleId]={BUNDLE}")
if not app or not app["data"]:
    sys.exit("app not found")
app_id = app["data"][0]["id"]
print("app", app_id)

gcd = call("GET", f"/apps/{app_id}/gameCenterDetail")
gcd_id = gcd["data"]["id"] if gcd and gcd.get("data") else None
if not gcd_id:
    made = call("POST", "/gameCenterDetails", {"data": {"type": "gameCenterDetails", "relationships": {"app": {"data": {"type": "apps", "id": app_id}}}}})
    gcd_id = made["data"]["id"] if made else None
print("gameCenterDetail", gcd_id)
if not gcd_id:
    sys.exit("no game center detail")

existing = {}
page = call("GET", f"/gameCenterDetails/{gcd_id}/gameCenterLeaderboards?limit=200")
for lb in (page or {}).get("data", []):
    existing[lb["attributes"].get("vendorIdentifier")] = lb["id"]

for vid, ref, en, ar, sort in BOARDS:
    if vid in existing:
        print("exists", vid)
        continue
    made = call("POST", "/gameCenterLeaderboards", {"data": {"type": "gameCenterLeaderboards",
        "attributes": {"referenceName": ref, "vendorIdentifier": vid, "defaultFormatter": "INTEGER",
                       "submissionType": "BEST_SCORE", "scoreSortType": sort},
        "relationships": {"gameCenterDetail": {"data": {"type": "gameCenterDetails", "id": gcd_id}}}}})
    if not made:
        continue
    lid = made["data"]["id"]
    for loc, name in (("en-US", en), ("ar-SA", ar)):
        call("POST", "/gameCenterLeaderboardLocalizations", {"data": {"type": "gameCenterLeaderboardLocalizations",
             "attributes": {"locale": loc, "name": name},
             "relationships": {"gameCenterLeaderboard": {"data": {"type": "gameCenterLeaderboards", "id": lid}}}}})
    print("created", vid)
print("done")
