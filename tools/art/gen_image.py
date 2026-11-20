"""Image generation for Arab Drift (copied from Sands of Empire; OpenAI Images API).

Every image is generated from a spec in art/prompts/*.json, which adds the shared style header from the style bible.
Outputs go to art/generated/<asset_id>/<asset_id>__vNN.png with a sidecar .json recording model, prompt and params,
so any image can be traced and regenerated.

Usage:
    python tools/art/gen_image.py art/prompts/heroes.json [--only hero_shaykh_fahad] [--n 2]
Needs OPENAI_API_KEY in the environment.
"""
import argparse
import base64
import json
import os
import sys
import time
import urllib.request

import requests

API = "https://api.openai.com/v1/images/generations"
EDIT_API = "https://api.openai.com/v1/images/edits"


def post_json(url, body, key, timeout=300):
    req = urllib.request.Request(url, data=json.dumps(body).encode(), method="POST",
                                 headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"OpenAI error {e.code}: {e.read().decode()[:600]}")


def build_prompt(spec, item):
    parts = [spec["style_header"], item["prompt"]]
    if spec.get("must_not") or item.get("must_not"):
        parts.append("Do not include: " + "; ".join(spec.get("must_not", []) + item.get("must_not", [])) + ".")
    return "\n\n".join(parts)


def next_version(folder, asset_id):
    os.makedirs(folder, exist_ok=True)
    n = 1
    while os.path.exists(os.path.join(folder, f"{asset_id}__v{n:02d}.png")):
        n += 1
    return n


def generate(a, item, body, key):
    refs = [os.path.join(a.repo, r) for r in item.get("reference_images", [])]
    if refs:
        files = [("image[]", (os.path.basename(r), open(r, "rb"), "image/png")) for r in refs]
        form = {k: str(v) for k, v in body.items()}
        r = requests.post(EDIT_API, headers={"Authorization": f"Bearer {key}"}, data=form, files=files, timeout=600)
        if r.status_code != 200:
            raise RuntimeError(f"OpenAI error {r.status_code}: {r.text[:600]}")
        return r.json()
    return post_json(API, body, key)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("spec")
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--n", type=int, default=None, help="candidates per asset (default from spec)")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--skip-existing", action="store_true", help="skip assets that already have a generated image (resume)")
    a = ap.parse_args()
    key = os.environ.get("OPENAI_API_KEY")
    if not key:
        sys.exit("OPENAI_API_KEY is not set")

    spec = json.load(open(a.spec, encoding="utf-8"))
    for item in spec["items"]:
        if a.only and item["asset_id"] not in a.only:
            continue
        if a.skip_existing and os.path.isdir(os.path.join(a.repo, "art/generated", item["asset_id"])):
            continue
        prompt = build_prompt(spec, item)
        params = {**spec.get("defaults", {}), **item.get("params", {})}
        n = a.n or params.pop("n", 1)
        params.pop("n", None)
        body = {"prompt": prompt, "n": n, **params}
        t0 = time.time()
        try:
            res = generate(a, item, body, key)
        except RuntimeError as e:
            # One refused or failed item must not stop the batch; quota errors still stop it.
            print(f"FAILED {item['asset_id']}: {str(e)[:300]}", flush=True)
            if "insufficient_quota" in str(e) or "credit_balance" in str(e):
                sys.exit(1)
            continue
        folder = os.path.join(a.repo, "art/generated", item["asset_id"])
        for d in res["data"]:
            v = next_version(folder, item["asset_id"])
            png = os.path.join(folder, f"{item['asset_id']}__v{v:02d}.png")
            with open(png, "wb") as f:
                f.write(base64.b64decode(d["b64_json"]))
            meta = {"asset_id": item["asset_id"], "version": v, "spec": os.path.relpath(a.spec, a.repo), "prompt": prompt,
                    "params": params, "reference_images": item.get("reference_images", []), "revised_prompt": d.get("revised_prompt"), "usage": res.get("usage"),
                    "seconds": round(time.time() - t0, 1), "created": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
                    "qa_status": "in_review"}
            with open(png[:-4] + ".json", "w", encoding="utf-8") as f:
                json.dump(meta, f, ensure_ascii=False, indent=1)
            print("wrote", png)


if __name__ == "__main__":
    main()
