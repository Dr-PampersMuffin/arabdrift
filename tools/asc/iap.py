"""Create the game's in-app purchases in App Store Connect (idempotent): four pearl packs (consumable) and the starter
pack (non-consumable), with English and Arabic names, US base price, all territories, and the review screenshot.

Runs in CI (.github/workflows/iap.yml, tag iap-*) with the App Store Connect API key from repository secrets:
APPSTORE_KEY_ID, APPSTORE_ISSUER_ID, APPSTORE_P8. Prints a short report; never prints the key.
"""
import hashlib, json, os, sys, time

import jwt
import requests

BUNDLE = "world.embur.arabdrift"
API = "https://api.appstoreconnect.apple.com"
SHOT = "art/asc/iap_review.png"
PRE = BUNDLE + "."
PRODUCTS = [
    # product id, type, price (USD), reference name, en name, en desc, ar name, ar desc
    (PRE + "pearls_100", "CONSUMABLE", "0.99", "Pearls 100", "100 Pearls", "Pearls for paint, rims and stickers.", "١٠٠ لؤلؤة", "لؤلؤ للصبغ والجنوط والملصقات."),
    (PRE + "pearls_550", "CONSUMABLE", "4.99", "Pearls 550", "550 Pearls", "Pearls for paint, rims and stickers.", "٥٥٠ لؤلؤة", "لؤلؤ للصبغ والجنوط والملصقات."),
    (PRE + "pearls_1200", "CONSUMABLE", "9.99", "Pearls 1200", "1,200 Pearls", "Pearls for paint, rims and stickers.", "١٢٠٠ لؤلؤة", "لؤلؤ للصبغ والجنوط والملصقات."),
    (PRE + "pearls_2600", "CONSUMABLE", "19.99", "Pearls 2600", "2,600 Pearls", "Pearls for paint, rims and stickers.", "٢٦٠٠ لؤلؤة", "لؤلؤ للصبغ والجنوط والملصقات."),
    (PRE + "starter_pack", "NON_CONSUMABLE", "1.99", "Starter Pack", "Starter Pack", "500 pearls, gold rims, Kuwait flag smoke.", "عرض البداية", "٥٠٠ لؤلؤة وجنوط ذهبية ودخان علم الكويت."),
]
REVIEW_NOTE = ("Pearls are a cosmetic currency: they buy paint, rims, stickers, windscreen slogans, body kits, coloured tyre smoke "
               "and neon in the garage shop (Garage > التعديل). Nothing that changes car performance is sold for money. "
               "The starter pack is a one-time purchase and can be restored from the pearl page (رجّع مشترياتي).")


def token():
    key = os.environ["APPSTORE_P8"].replace("\\n", "\n")
    now = int(time.time())
    return jwt.encode({"iss": os.environ["APPSTORE_ISSUER_ID"].strip(), "iat": now, "exp": now + 1100, "aud": "appstoreconnect-v1"},
                      key, algorithm="ES256", headers={"kid": os.environ["APPSTORE_KEY_ID"].strip(), "typ": "JWT"})


H = {"Authorization": "Bearer " + token(), "Content-Type": "application/json"}


def call(method, path, body=None, quiet=False):
    r = requests.request(method, path if path.startswith("http") else API + path, headers=H, data=json.dumps(body) if body is not None else None, timeout=60)
    if r.status_code >= 300:
        if not quiet:
            print(f"{method} {path.split('?')[0]} -> {r.status_code}: {r.text[:500]}")
        return None
    return r.json() if r.text else {}


app = call("GET", f"/v1/apps?filter[bundleId]={BUNDLE}")
if not app or not app["data"]:
    sys.exit("app not found")
app_id = app["data"][0]["id"]
print("app", app_id)

existing = {}
page = call("GET", f"/v1/apps/{app_id}/inAppPurchasesV2?limit=200")
for it in (page or {}).get("data", []):
    existing[it["attributes"]["productId"]] = it["id"]

terr = call("GET", "/v1/territories?limit=200")
territories = [{"type": "territories", "id": t["id"]} for t in (terr or {}).get("data", [])]
print("territories", len(territories))

for pid, kind, usd, ref, en, en_d, ar, ar_d in PRODUCTS:
    iid = existing.get(pid)
    if not iid:
        made = call("POST", "/v2/inAppPurchases", {"data": {"type": "inAppPurchases",
            "attributes": {"name": ref, "productId": pid, "inAppPurchaseType": kind, "reviewNote": REVIEW_NOTE, "familySharable": False},
            "relationships": {"app": {"data": {"type": "apps", "id": app_id}}}}})
        if not made:
            print("FAILED create", pid)
            continue
        iid = made["data"]["id"]
        print("created", pid, iid)
    else:
        print("exists", pid, iid)

    # names (English + Arabic)
    have = {l["attributes"]["locale"] for l in (call("GET", f"/v2/inAppPurchases/{iid}/inAppPurchaseLocalizations?limit=50") or {}).get("data", [])}
    for loc, name, desc in (("en-US", en, en_d), ("ar-SA", ar, ar_d)):
        if loc in have:
            continue
        ok = call("POST", "/v1/inAppPurchaseLocalizations", {"data": {"type": "inAppPurchaseLocalizations",
            "attributes": {"locale": loc, "name": name, "description": desc},
            "relationships": {"inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": iid}}}}})
        print("  name", loc, "ok" if ok else "FAILED")

    # price: the US price point, Apple fills the other countries
    sched = call("GET", f"/v2/inAppPurchases/{iid}/iapPriceSchedule", quiet=True)
    if not sched or not sched.get("data"):
        pp, url = None, f"{API}/v2/inAppPurchases/{iid}/pricePoints?filter[territory]=USA&limit=200"
        while url and not pp:
            res = call("GET", url) or {}
            for p in res.get("data", []):
                if p["attributes"].get("customerPrice") == usd:
                    pp = p["id"]
                    break
            url = (res.get("links") or {}).get("next")
        if pp:
            ok = call("POST", "/v1/inAppPurchasePriceSchedules", {"data": {"type": "inAppPurchasePriceSchedules",
                "relationships": {"inAppPurchase": {"data": {"type": "inAppPurchases", "id": iid}},
                                  "baseTerritory": {"data": {"type": "territories", "id": "USA"}},
                                  "manualPrices": {"data": [{"type": "inAppPurchasePrices", "id": "${price0}"}]}}},
                "included": [{"type": "inAppPurchasePrices", "id": "${price0}", "attributes": {"startDate": None},
                              "relationships": {"inAppPurchasePricePoint": {"data": {"type": "inAppPurchasePricePoints", "id": pp}}}}]})
            print("  price", usd, "ok" if ok else "FAILED")
        else:
            print("  price point not found for", usd)
    else:
        print("  price already set")

    # sold everywhere the app is
    av = call("GET", f"/v2/inAppPurchases/{iid}/inAppPurchaseAvailability", quiet=True)
    if not av or not av.get("data"):
        ok = call("POST", "/v1/inAppPurchaseAvailabilities", {"data": {"type": "inAppPurchaseAvailabilities",
            "attributes": {"availableInNewTerritories": True},
            "relationships": {"inAppPurchase": {"data": {"type": "inAppPurchases", "id": iid}},
                              "availableTerritories": {"data": territories}}}})
        print("  availability", "ok" if ok else "FAILED")
    else:
        print("  availability already set")

    # review screenshot (the shop screen)
    shot = call("GET", f"/v2/inAppPurchases/{iid}/appStoreReviewScreenshot", quiet=True)
    if (not shot or not shot.get("data")) and os.path.exists(SHOT):
        data = open(SHOT, "rb").read()
        res = call("POST", "/v1/inAppPurchaseAppStoreReviewScreenshots", {"data": {"type": "inAppPurchaseAppStoreReviewScreenshots",
            "attributes": {"fileName": "shop.png", "fileSize": len(data)},
            "relationships": {"inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": iid}}}}})
        if res:
            sid = res["data"]["id"]
            for op in res["data"]["attributes"].get("uploadOperations", []):
                hdr = {h["name"]: h["value"] for h in op.get("requestHeaders", [])}
                part = data[op["offset"]:op["offset"] + op["length"]]
                requests.request(op["method"], op["url"], headers=hdr, data=part, timeout=120)
            ok = call("PATCH", f"/v1/inAppPurchaseAppStoreReviewScreenshots/{sid}", {"data": {"type": "inAppPurchaseAppStoreReviewScreenshots", "id": sid,
                "attributes": {"uploaded": True, "sourceFileChecksum": hashlib.md5(data).hexdigest()}}})
            print("  screenshot", "ok" if ok else "FAILED")
    elif shot and shot.get("data"):
        print("  screenshot already set")

# final state
page = call("GET", f"/v1/apps/{app_id}/inAppPurchasesV2?limit=200")
for it in (page or {}).get("data", []):
    a = it["attributes"]
    print("STATE", a["productId"], a.get("state"))
print("done")
