"""Fetch CC0 (public domain) photo-scanned PBR materials and an HDR sky from Poly Haven into Resources/DriftKuwait/Tex.
Runs on the CI build machine (which has internet); the game falls back to its own textures when a file is missing.
Every asset on Poly Haven is CC0: free for commercial use, no attribution required (we credit them anyway)."""
import json, os, sys, urllib.request

OUT = os.path.join(os.path.dirname(__file__), "../../client/Assets/DriftKuwait/Resources/DriftKuwait/Tex")
API = "https://api.polyhaven.com"
UA = {"User-Agent": "q8drift-build/1.0 (+https://embur.world)"}

# our name -> (candidate ids in order of preference, category to search if none of them exist)
TEX = {
    "pbr_asphalt": (["asphalt_02", "asphalt_track", "rough_asphalt", "asphalt_01"], "asphalt"),
    "pbr_plaster": (["beige_wall_001", "plastered_wall_02", "painted_plaster_wall", "rough_plaster_02"], "plaster"),
    "pbr_paving": (["patterned_paving_02", "paving_stones_04", "patterned_concrete_pavers", "square_floor_patern_01"], "floor"),
    "pbr_sand": (["coast_sand_01", "sand_01", "aerial_beach_01", "sandy_gravel_02"], "sand"),
    "pbr_concrete": (["concrete_floor_02", "concrete_wall_008", "concrete_floor_worn_001"], "concrete"),
}
HDRI = (["kloofendal_48d_partly_cloudy_puresky", "kloofendal_43d_clear_puresky", "qwantani_puresky", "syferfontein_0d_clear_puresky"], "skies")


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return r.read()


def files(asset_id):
    try:
        return json.loads(get(f"{API}/files/{asset_id}"))
    except Exception:
        return None


def search(kind, cat):
    try:
        d = json.loads(get(f"{API}/assets?t={kind}&c={cat}"))
        ids = sorted(d, key=lambda k: -d[k].get("download_count", 0))
        if kind == "hdris":
            ids = [i for i in ids if "puresky" in i] + ids
        return ids[:6]
    except Exception:
        return []


def pick(fdict, key, res, fmt):
    m = fdict.get(key) or {}
    for r in [res, "2k", "1k", "4k"]:
        if r in m and fmt in m[r]:
            return m[r][fmt]["url"]
    return None


def save(url, name):
    data = get(url)
    p = os.path.join(OUT, name)
    open(p, "wb").write(data)
    print(f"  {name} {len(data)//1024} KB")


def main():
    os.makedirs(OUT, exist_ok=True)
    credits = []
    for ours, (cands, cat) in TEX.items():
        done = False
        for aid in cands + search("textures", cat):
            f = files(aid)
            if not f:
                continue
            d = pick(f, "Diffuse", "2k", "jpg")
            n = pick(f, "nor_gl", "2k", "jpg") or pick(f, "nor_gl", "2k", "png")
            if not d:
                continue
            print(ours, "<-", aid)
            save(d, ours + ".jpg")
            if n:
                save(n, ours + "_n" + os.path.splitext(n)[1])
            credits.append(aid)
            done = True
            break
        if not done:
            print(ours, "not available, the game keeps its own texture")
    for aid in HDRI[0] + search("hdris", HDRI[1]):
        f = files(aid)
        u = f and pick(f, "hdri", "2k", "hdr")
        if u:
            print("sky_hdri <-", aid)
            save(u, "sky_hdri.hdr")
            credits.append(aid)
            break
    open(os.path.join(OUT, "cc0_credits.txt"), "w").write("Poly Haven (CC0): " + ", ".join(credits) + "\n")


if __name__ == "__main__":
    try:
        main()
    except Exception as e:   # never fail the build over optional art
        print("fetch_cc0 failed:", e)
    sys.exit(0)
