"""Tripo 3D pipeline for Sands of Empire: concept image -> 3D model -> rig -> animations.

Uses the Tripo OpenAPI v2 (api.tripo3d.ai). Every step writes its outputs and a log entry (task id, credits used)
to art/models/<asset_id>/, so nothing is lost and every credit is accounted for.

Usage:
  python tools/art/tripo.py model  <asset_id> <image.png> [--version v2.5-20250123] [--faces 12000]
  python tools/art/tripo.py rig    <asset_id> [--spec tripo|mixamo]
  python tools/art/tripo.py anim   <asset_id> preset:idle preset:walk ...
  python tools/art/tripo.py balance
Needs TRIPO_API_KEY in the environment.
"""
import argparse
import json
import os
import sys
import time

import requests

BASE = "https://api.tripo3d.ai/v2/openapi"
ROOT = os.getcwd()


def H():
    key = os.environ.get("TRIPO_API_KEY")
    if not key:
        sys.exit("TRIPO_API_KEY is not set")
    return {"Authorization": f"Bearer {key}"}


def call(method, path, **kw):
    # Retries only idempotent GETs: the proxy occasionally resets connections. POSTs are never retried (no double charge).
    for attempt in range(5):
        try:
            r = requests.request(method, BASE + path, headers=H(), timeout=120, **kw)
            break
        except requests.exceptions.ConnectionError:
            if method != "GET" or attempt == 4:
                raise
            time.sleep(2 + 2 * attempt)
    try:
        body = r.json()
    except ValueError:
        sys.exit(f"Tripo {r.status_code}: {r.text[:400]}")
    if r.status_code != 200 or body.get("code") != 0:
        sys.exit(f"Tripo error {r.status_code}: {json.dumps(body)[:600]}")
    return body["data"]


def balance():
    return call("GET", "/user/balance")["balance"]


def folder(asset_id):
    p = os.path.join(ROOT, "art/models", asset_id)
    os.makedirs(p, exist_ok=True)
    return p


def log(asset_id, entry):
    path = os.path.join(folder(asset_id), "tripo_log.json")
    data = json.load(open(path)) if os.path.exists(path) else []
    data.append(entry)
    json.dump(data, open(path, "w"), indent=1)


def last(asset_id, step):
    path = os.path.join(folder(asset_id), "tripo_log.json")
    for e in reversed(json.load(open(path)) if os.path.exists(path) else []):
        if e["step"] == step and e["status"] == "success":
            return e
    sys.exit(f"No successful '{step}' task for {asset_id} yet")


def run_task(asset_id, step, body, outputs):
    """Creates a task, polls to completion, downloads outputs, logs task id and credits used."""
    before = balance()
    task_id = call("POST", "/task", json=body)["task_id"]
    print(f"[{asset_id}] {step}: task {task_id}", flush=True)
    t0 = time.time()
    while True:
        d = call("GET", f"/task/{task_id}")
        st = d["status"]
        if st in ("success", "failed", "cancelled", "banned", "expired", "unknown"):
            break
        if time.time() - t0 > 1200:
            st = "timeout"
            break
        time.sleep(3)
    files = {}
    if st == "success":
        out = d.get("output", {})
        for key, name in outputs.items():
            url = out.get(key)
            if not url:
                continue
            ext = url.split("?")[0].rsplit(".", 1)[-1][:5]
            dest = os.path.join(folder(asset_id), f"{name}.{ext}")
            for attempt in range(5):
                try:
                    with requests.get(url, timeout=300, stream=True) as r:
                        r.raise_for_status()
                        with open(dest, "wb") as f:
                            for chunk in r.iter_content(1 << 20):
                                f.write(chunk)
                    break
                except requests.exceptions.ConnectionError:
                    if attempt == 4:
                        raise
                    time.sleep(2 + 2 * attempt)
            files[key] = os.path.relpath(dest, ROOT)
    after = balance()
    entry = {"step": step, "task_id": task_id, "status": st, "credits": before - after, "seconds": round(time.time() - t0),
             "body": {k: v for k, v in body.items() if k != "file"}, "files": files,
             "output_meta": {k: v for k, v in d.get("output", {}).items() if not str(v).startswith("http")},
             "time": time.strftime("%Y-%m-%dT%H:%M:%S%z")}
    log(asset_id, entry)
    print(f"[{asset_id}] {step}: {st}, {before - after} credits, files {list(files.values())}", flush=True)
    if st != "success":
        sys.exit(1)
    return entry


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("model")
    m.add_argument("asset_id")
    m.add_argument("image")
    m.add_argument("--version", default="v2.5-20250123")
    m.add_argument("--faces", type=int, default=None)
    m.add_argument("--texture-quality", default="detailed", choices=["standard", "detailed"])
    m.add_argument("--geometry-quality", default=None, choices=["standard", "detailed"], help="v3.x only")
    r = sub.add_parser("rig")
    r.add_argument("asset_id")
    r.add_argument("--spec", default="tripo", choices=["tripo", "mixamo"])
    r.add_argument("--rig-version", default=None, help="e.g. v2.5-20260210 (needed for quadrupeds)")
    r.add_argument("--rig-type", default=None, help="override the pre-rig check, e.g. quadruped for mounted units")
    a = sub.add_parser("anim")
    a.add_argument("asset_id")
    a.add_argument("presets", nargs="+")
    sub.add_parser("balance")
    args = ap.parse_args()

    if args.cmd == "balance":
        print("balance:", balance())
        return

    if args.cmd == "model":
        with open(args.image, "rb") as f:
            token = call("POST", "/upload", files={"file": (os.path.basename(args.image), f, "image/png")})["image_token"]
        body = {"type": "image_to_model", "model_version": args.version,
                "file": {"type": args.image.rsplit(".", 1)[-1].lower(), "file_token": token},
                "texture": True, "pbr": True, "texture_quality": args.texture_quality}
        if args.faces:
            body["face_limit"] = args.faces
        if args.geometry_quality:
            body["geometry_quality"] = args.geometry_quality
        run_task(args.asset_id, "model", body, {"pbr_model": "model_pbr", "model": "model", "base_model": "model_base",
                                               "rendered_image": "preview"})
    elif args.cmd == "rig":
        src = last(args.asset_id, "model")["task_id"]
        chk = run_task(args.asset_id, "prerigcheck", {"type": "animate_prerigcheck", "original_model_task_id": src}, {})
        print("prerigcheck:", chk["output_meta"])
        if chk["output_meta"].get("riggable") is False and not args.rig_type:
            sys.exit("Model is not riggable")
        body = {"type": "animate_rig", "original_model_task_id": src, "out_format": "glb", "spec": args.spec}
        if chk["output_meta"].get("rig_type"):
            body["rig_type"] = chk["output_meta"]["rig_type"]
        if args.rig_type:
            body["rig_type"] = args.rig_type
        if args.rig_version:
            body["model_version"] = args.rig_version
        run_task(args.asset_id, "rig", body, {"model": "rigged"})
    elif args.cmd == "anim":
        src = last(args.asset_id, "rig")["task_id"]
        for p in args.presets:
            run_task(args.asset_id, f"anim:{p}", {"type": "animate_retarget", "original_model_task_id": src,
                                                  "animation": p, "out_format": "glb", "bake_animation": True},
                     {"model": "anim_" + p.split(":")[-1]})


if __name__ == "__main__":
    main()
