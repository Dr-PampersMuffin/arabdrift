#!/usr/bin/env python3
"""
Fills the Q8 Drift App Store listing in App Store Connect from tools/asc/listing.json (ported from Sands of Empire):
  app name, subtitle and privacy URL (English and Arabic), category, age rating answers, the version's description,
  keywords, promotional text, support URL and copyright, review notes, and the screenshots in store/screenshots.
Anything already equal is left alone. App Privacy ("nutrition label") answers cannot be set through Apple's API;
they are answered on the website (see docs/APP_STORE.md).

Env: ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_PATH, BUNDLE_ID, optional APP_VERSION (default 1.0.0),
     optional REVIEW_FIRST_NAME, REVIEW_LAST_NAME, REVIEW_PHONE, REVIEW_EMAIL (App Review contact).
Usage: python3 tools/appstore/listing.py [--dry-run] [--screenshots] [--attach-build] [--pricing]
"""
import glob
import hashlib
import json
import time
import re
import os
import sys

import jwt
import requests

API = "https://api.appstoreconnect.apple.com"


class Asc:
    def __init__(self, key_id, issuer, key_path, dry):
        self.key_id, self.issuer, self.key, self.dry = key_id, issuer, open(key_path).read(), dry
        self.s = requests.Session()
        self._tok, self._exp = "", 0

    def token(self):
        if time.time() > self._exp - 60:
            self._exp = int(time.time()) + 1100
            self._tok = jwt.encode({"iss": self.issuer, "exp": self._exp, "aud": "appstoreconnect-v1"}, self.key,
                                   algorithm="ES256", headers={"kid": self.key_id, "typ": "JWT"})
        return self._tok

    def call(self, method, path, body=None, params=None):
        url = path if path.startswith("http") else API + path
        if self.dry and method != "GET":
            print(f"  [dry] {method} {path} {json.dumps(body)[:200] if body else ''}")
            return {"data": {"id": "dry"}}
        r = self.s.request(method, url, json=body, params=params, headers={"Authorization": "Bearer " + self.token()})
        if r.status_code >= 300:
            raise RuntimeError(f"{method} {path} -> {r.status_code}: {r.text[:1500]}")
        return r.json() if r.text else {}

    def all(self, path, params=None):
        out, url = [], path
        while url:
            j = self.call("GET", url, params=params)
            out += j.get("data", [])
            url, params = j.get("links", {}).get("next"), None
        return out



ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EDITABLE = {"PREPARE_FOR_SUBMISSION", "DEVELOPER_REJECTED", "REJECTED", "METADATA_REJECTED", "INVALID_BINARY"}
DISPLAY = {"iphone": "APP_IPHONE_67", "ipad": "APP_IPAD_PRO_3GEN_129"}  # ipad used only if store/screenshots/<locale>/ipad has files
SIZES = {"iphone": {(2868, 1320), (1320, 2868)}, "ipad": {(2752, 2064), (2064, 2752)}}


def upsert_localization(asc, kind, rel, parent_id, locale, want, existing):
    """kind: appInfoLocalizations or appStoreVersionLocalizations."""
    cur = existing.get(locale)
    if cur:
        a = cur["attributes"]
        diff = {k: v for k, v in want.items() if a.get(k) != v}
        if diff:
            asc.call("PATCH", f"/v1/{kind}/{cur['id']}", {"data": {"type": kind, "id": cur["id"], "attributes": diff}})
            print(f"  {kind} {locale}: updated {sorted(diff)}")
        else:
            print(f"  {kind} {locale}: already up to date")
        return cur["id"]
    j = asc.call("POST", f"/v1/{kind}", {"data": {"type": kind, "attributes": dict(want, locale=locale),
                                                  "relationships": {rel: {"data": {"type": rel + "s", "id": parent_id}}}}})
    print(f"  {kind} {locale}: created")
    return j["data"]["id"]


def age_rating(asc, app_info_id, answers, extra=None):
    extra = extra or {}
    decl = asc.call("GET", f"/v1/appInfos/{app_info_id}/ageRatingDeclaration")["data"]
    cur = decl["attributes"]
    print("  age rating now:", json.dumps(cur, sort_keys=True))
    # Only answer questions Apple's API actually has (it gains new ones over time; unknown keys are rejected).
    want = {k: v for k, v in answers.items() if k in cur}
    skipped = sorted(set(answers) - set(want))
    if skipped:
        print("  age rating: not in this API version, answer on the website:", skipped)
    # Frequency answers use NONE / INFREQUENT_OR_MILD / FREQUENT_OR_INTENSE in older API versions and
    # NONE / INFREQUENT / FREQUENT in newer ones: follow whichever style the current answers use.
    newer = any(v in ("INFREQUENT", "FREQUENT") for v in cur.values() if isinstance(v, str))
    style = {"INFREQUENT_OR_MILD": "INFREQUENT", "FREQUENT_OR_INTENSE": "FREQUENT"} if newer else {}
    want = {k: style.get(v, v) if isinstance(v, str) else v for k, v in want.items()}
    diff = {k: v for k, v in want.items() if cur.get(k) != v}
    if not diff:
        print("  age rating: already answered")
        return
    try:
        for _ in range(6):
            try:
                asc.call("PATCH", f"/v1/ageRatingDeclarations/{decl['id']}", {"data": {"type": "ageRatingDeclarations", "id": decl["id"], "attributes": diff}})
                break
            except RuntimeError as e:
                # Apple asks for a follow-up answer only in some cases (e.g. socialMediaAgeRestricted): add it if we have it.
                m = re.search(r"attribute '(\w+)'", str(e))
                if "REQUIRED" in str(e) and m and m.group(1) in extra and m.group(1) not in diff:
                    diff[m.group(1)] = extra[m.group(1)]
                    print("  age rating: Apple also asks", m.group(1))
                    continue
                raise
    except RuntimeError as e:
        if "INFREQUENT" not in str(e) and "ENUM" not in str(e).upper():
            raise
        flip = {"INFREQUENT_OR_MILD": "INFREQUENT", "FREQUENT_OR_INTENSE": "FREQUENT", "INFREQUENT": "INFREQUENT_OR_MILD", "FREQUENT": "FREQUENT_OR_INTENSE"}
        diff = {k: flip.get(v, v) if isinstance(v, str) else v for k, v in diff.items()}
        asc.call("PATCH", f"/v1/ageRatingDeclarations/{decl['id']}", {"data": {"type": "ageRatingDeclarations", "id": decl["id"], "attributes": diff}})
    print("  age rating: set", sorted(diff))


def editable_version(asc, app_id, version):
    versions = asc.all(f"/v1/apps/{app_id}/appStoreVersions", {"filter[platform]": "IOS", "limit": 50})
    for v in versions:
        if v["attributes"]["appStoreState"] in EDITABLE or v["attributes"].get("appVersionState") in ("PREPARE_FOR_SUBMISSION", "DEVELOPER_REJECTED", "REJECTED", "METADATA_REJECTED"):
            if v["attributes"]["versionString"] != version:
                asc.call("PATCH", f"/v1/appStoreVersions/{v['id']}", {"data": {"type": "appStoreVersions", "id": v["id"], "attributes": {"versionString": version}}})
                print(f"  version {v['attributes']['versionString']} renamed to {version}")
            return v["id"]
    j = asc.call("POST", "/v1/appStoreVersions", {"data": {"type": "appStoreVersions", "attributes": {"platform": "IOS", "versionString": version},
                                                         "relationships": {"app": {"data": {"type": "apps", "id": app_id}}}}})
    print("  version created", version)
    return j["data"]["id"]


def upload_screenshots(asc, loc_id, locale):
    for device, display in DISPLAY.items():
        base = os.environ.get("SHOTS_DIR") or os.path.join(ROOT, "store", "screenshots", locale, device)
        files = sorted(glob.glob(os.path.join(base, "*.png")) + glob.glob(os.path.join(base, "*.jpg"))) if device == "iphone" or not os.environ.get("SHOTS_DIR") else []
        if not files:
            print(f"  screenshots {locale} {device}: none in store/screenshots/{locale}/{device}")
            continue
        from PIL import Image
        for f in files:
            size = Image.open(f).size
            if size not in SIZES[device]:
                raise SystemExit(f"{f} is {size}, needs one of {SIZES[device]}")
        sets = asc.all(f"/v1/appStoreVersionLocalizations/{loc_id}/appScreenshotSets")
        sset = next((s for s in sets if s["attributes"]["screenshotDisplayType"] == display), None)
        if not sset:
            sset = asc.call("POST", "/v1/appScreenshotSets", {"data": {"type": "appScreenshotSets", "attributes": {"screenshotDisplayType": display},
                                                                      "relationships": {"appStoreVersionLocalization": {"data": {"type": "appStoreVersionLocalizations", "id": loc_id}}}}})["data"]
        # Replace what is there with this set, in file-name order.
        for old in asc.all(f"/v1/appScreenshotSets/{sset['id']}/appScreenshots"):
            asc.call("DELETE", f"/v1/appScreenshots/{old['id']}")
        for f in files:
            data = open(f, "rb").read()
            shot = asc.call("POST", "/v1/appScreenshots", {"data": {"type": "appScreenshots", "attributes": {"fileName": os.path.basename(f), "fileSize": len(data)},
                                                                   "relationships": {"appScreenshotSet": {"data": {"type": "appScreenshotSets", "id": sset["id"]}}}}})["data"]
            if asc.dry:
                continue
            for op in shot["attributes"]["uploadOperations"]:
                part = data[op["offset"]: op["offset"] + op["length"]]
                r = asc.s.request(op["method"], op["url"], data=part, headers={h["name"]: h["value"] for h in op["requestHeaders"]})
                if r.status_code >= 300:
                    raise RuntimeError(f"upload {f}: {r.status_code} {r.text[:300]}")
            asc.call("PATCH", f"/v1/appScreenshots/{shot['id']}", {"data": {"type": "appScreenshots", "id": shot["id"],
                                                                            "attributes": {"uploaded": True, "sourceFileChecksum": hashlib.md5(data).hexdigest()}}})
        print(f"  screenshots {locale} {device}: {len(files)} uploaded")
        if not asc.dry:
            # Apple checks each image after upload: report anything it rejects (wrong size for this display, etc.).
            for _ in range(12):
                time.sleep(10)
                states = [(x["attributes"].get("fileName"), (x["attributes"].get("assetDeliveryState") or {}))
                          for x in asc.all(f"/v1/appScreenshotSets/{sset['id']}/appScreenshots")]
                if all(st.get("state") in ("COMPLETE", "FAILED") for _, st in states):
                    break
            for name, st in states:
                print(f"    {name}: {st.get('state')} {st.get('errors') or ''}")


def pricing(asc, app_id):
    """The app itself is free (US base price 0), available in every territory."""
    try:
        cur = asc.call("GET", f"/v1/apps/{app_id}/appPriceSchedule").get("data")
    except RuntimeError:
        cur = None
    if cur:
        print("  price schedule: already set")
    else:
        try:
            pts = asc.all(f"/v1/apps/{app_id}/appPricePoints", {"filter[territory]": "USA", "limit": 200})
            free = next(p for p in pts if float(p["attributes"]["customerPrice"]) == 0.0)
            asc.call("POST", "/v1/appPriceSchedules", {
                "data": {"type": "appPriceSchedules", "relationships": {
                    "app": {"data": {"type": "apps", "id": app_id}},
                    "baseTerritory": {"data": {"type": "territories", "id": "USA"}},
                    "manualPrices": {"data": [{"type": "appPrices", "id": "${p0}"}]}}},
                "included": [{"type": "appPrices", "id": "${p0}", "attributes": {"startDate": None},
                              "relationships": {"appPricePoint": {"data": {"type": "appPricePoints", "id": free["id"]}}}}]})
            print("  price schedule: Free")
        except Exception as e:  # noqa: BLE001
            print("  price schedule not set, choose Free on the website:", str(e)[:300])
    try:
        av = asc.call("GET", f"/v1/apps/{app_id}/appAvailabilityV2").get("data")
        print("  availability:", "set" if av else "not set")
    except RuntimeError as e:
        terr = [t["id"] for t in asc.all("/v1/territories", {"limit": 200})]
        try:
            asc.call("POST", "/v2/appAvailabilities", {
                "data": {"type": "appAvailabilities", "attributes": {"availableInNewTerritories": True},
                         "relationships": {"app": {"data": {"type": "apps", "id": app_id}},
                                           "territoryAvailabilities": {"data": [{"type": "territoryAvailabilities", "id": "${t%d}" % i} for i in range(len(terr))]}}},
                "included": [{"type": "territoryAvailabilities", "id": "${t%d}" % i, "attributes": {"available": True},
                              "relationships": {"territory": {"data": {"type": "territories", "id": t}}}} for i, t in enumerate(terr)]})
            print("  availability: all", len(terr), "territories")
        except Exception as e2:  # noqa: BLE001
            print("  availability not set, choose all countries on the website:", str(e2)[:300])


def iap_report(asc, app_id):
    for it in asc.all(f"/v1/apps/{app_id}/inAppPurchasesV2", {"limit": 200}):
        a = it["attributes"]
        print(f"  IAP {a.get('productId')}: {a.get('state')}")


def main():
    dry = "--dry-run" in sys.argv
    L = json.load(open(os.path.join(ROOT, "tools", "asc", "listing.json"), encoding="utf-8"))
    asc = Asc(os.environ["ASC_KEY_ID"], os.environ["ASC_ISSUER_ID"], os.environ["ASC_KEY_PATH"], dry)
    bundle = os.environ.get("BUNDLE_ID", "world.embur.arabdrift")
    version = os.environ.get("APP_VERSION", "1.0.0")
    for key in ("privacy_url", "terms_url", "support_url"):
        try:
            code = requests.get(L[key], timeout=30).status_code
        except Exception as e:  # noqa: BLE001
            code = str(e)[:80]
        print(f"page {L[key]} -> {code}")
    apps = asc.call("GET", "/v1/apps", params={"filter[bundleId]": bundle})["data"]
    if not apps:
        raise SystemExit(f"No app with bundle id {bundle} in App Store Connect")
    app_id = apps[0]["id"]
    print("app", app_id, apps[0]["attributes"]["name"], "primary locale", apps[0]["attributes"].get("primaryLocale"))

    # App information: name, subtitle, privacy policy URL, category, age rating.
    infos = asc.all(f"/v1/apps/{app_id}/appInfos")
    info = next((i for i in infos if i["attributes"].get("appStoreState", i["attributes"].get("state")) not in ("READY_FOR_SALE", "READY_FOR_DISTRIBUTION")), infos[0])
    have = {l["attributes"]["locale"]: l for l in asc.all(f"/v1/appInfos/{info['id']}/appInfoLocalizations")}
    for locale, t in L["locales"].items():
        want = {"name": t["name"], "subtitle": t["subtitle"], "privacyPolicyUrl": L["privacy_url"]}
        try:
            upsert_localization(asc, "appInfoLocalizations", "appInfo", info["id"], locale, want, have)
        except RuntimeError as e:
            print(f"  {locale}: name not accepted ({str(e)[:300]}); keeping the current name, setting the rest")
            want.pop("name")
            upsert_localization(asc, "appInfoLocalizations", "appInfo", info["id"], locale, want, have)
    asc.call("PATCH", f"/v1/appInfos/{info['id']}", {"data": {"type": "appInfos", "id": info["id"], "relationships": {
        "primaryCategory": {"data": {"type": "appCategories", "id": L["category"]["primary"]}},
        "primarySubcategoryOne": {"data": {"type": "appCategories", "id": L["category"]["sub1"]}},
        "primarySubcategoryTwo": {"data": {"type": "appCategories", "id": L["category"]["sub2"]}}}}})
    print("  category", L["category"])
    age_rating(asc, info["id"], L["age_rating"], L.get("age_rating_if_asked"))

    # The version: texts, copyright, review notes, screenshots.
    vid = editable_version(asc, app_id, version)
    copyright_ = L["copyright"].replace("{developer}", "Embur")
    asc.call("PATCH", f"/v1/appStoreVersions/{vid}", {"data": {"type": "appStoreVersions", "id": vid, "attributes": {"copyright": copyright_}}})
    print("  copyright", copyright_)
    have = {l["attributes"]["locale"]: l for l in asc.all(f"/v1/appStoreVersions/{vid}/appStoreVersionLocalizations")}
    loc_ids = {}
    for locale, t in L["locales"].items():
        loc_ids[locale] = upsert_localization(asc, "appStoreVersionLocalizations", "appStoreVersion", vid, locale, {
            "description": t["description"], "keywords": t["keywords"], "promotionalText": t["promotional_text"],
            "supportUrl": L["support_url"], "marketingUrl": L.get("marketing_url")}, have)

    contact = {k: os.environ.get(e) for k, e in (("contactFirstName", "REVIEW_FIRST_NAME"), ("contactLastName", "REVIEW_LAST_NAME"),
                                                 ("contactPhone", "REVIEW_PHONE"), ("contactEmail", "REVIEW_EMAIL"))}
    review = {"demoAccountRequired": False, "notes": L["review_notes"], **{k: v for k, v in contact.items() if v}}
    try:
        cur = asc.call("GET", f"/v1/appStoreVersions/{vid}/appStoreReviewDetail").get("data")
    except RuntimeError:
        cur = None
    if cur:
        try:
            asc.call("PATCH", f"/v1/appStoreReviewDetails/{cur['id']}", {"data": {"type": "appStoreReviewDetails", "id": cur["id"], "attributes": review}})
        except RuntimeError as e:
            # Apple wants the reviewer contact (name, phone, email) on every edit; until it is filled on the website, leave the notes as they are.
            print("  review details unchanged (add the App Review contact on the website):", str(e)[:160].replace("\n", " "))
    else:
        asc.call("POST", "/v1/appStoreReviewDetails", {"data": {"type": "appStoreReviewDetails", "attributes": review,
                                                               "relationships": {"appStoreVersion": {"data": {"type": "appStoreVersions", "id": vid}}}}})
    missing = [e for k, e in (("contactFirstName", "REVIEW_FIRST_NAME"), ("contactLastName", "REVIEW_LAST_NAME"), ("contactPhone", "REVIEW_PHONE"), ("contactEmail", "REVIEW_EMAIL")) if not contact[k]]
    print("  review notes set" + (f"; contact still to fill on the website ({', '.join(missing)})" if missing else "; contact set"))

    if "--pricing" in sys.argv:
        pricing(asc, app_id)
    iap_report(asc, app_id)

    if "--screenshots" in sys.argv:
        for locale, lid in loc_ids.items():
            upload_screenshots(asc, lid, locale)

    if "--attach-build" in sys.argv:
        builds = asc.call("GET", "/v1/builds", params={"filter[app]": app_id, "filter[preReleaseVersion.version]": version,
                                        "filter[processingState]": "VALID", "sort": "-uploadedDate", "limit": 1})["data"]
        if builds:
            asc.call("PATCH", f"/v1/appStoreVersions/{vid}/relationships/build", {"data": {"type": "builds", "id": builds[0]["id"]}})
            print("  build attached", builds[0]["attributes"]["version"])
        else:
            print(f"  no processed build for version {version} yet")
    print("listing done")


if __name__ == "__main__":
    main()
