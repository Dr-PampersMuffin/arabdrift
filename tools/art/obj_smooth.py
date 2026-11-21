"""Smooth the shading of a car body OBJ written by car_prep.py.

Tripo meshes are split along UV seams and carry slightly noisy normals, which makes painted panels look crumpled
under strong sun. This welds vertices by position, averages area-weighted face normals across the weld (keeping
real creases sharper than CREASE degrees), then relaxes the normals a few times over the surface.

  python3 tools/art/obj_smooth.py client/Assets/DriftKuwait/Resources/DriftKuwait/Cars/shaheen/body.obj
"""
import sys

import numpy as np

CREASE = 50.0
RELAX = 3


def main(path):
    V, T, F, head = [], [], [], []
    for line in open(path):
        if line.startswith("v "):
            V.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("vt "):
            T.append(line)
        elif line.startswith("f "):
            F.append([int(p.split("/")[0]) - 1 for p in line.split()[1:4]])
        elif not line.startswith("vn "):
            head.append(line)
    V = np.array(V)
    F = np.array(F)

    # Face normals (area-weighted) and weld groups by rounded position.
    a, b, c = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    fn = np.cross(b - a, c - a)
    fu = fn / (np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12)
    key = np.round(V / 1e-4).astype(np.int64)
    _, weld = np.unique(key, axis=0, return_inverse=True)
    weld = weld.ravel()
    G = weld.max() + 1

    # First pass: plain weld-average, used as the reference for the crease test.
    acc = np.zeros((G, 3))
    for k in range(3):
        np.add.at(acc, weld[F[:, k]], fn)
    ref = acc / (np.linalg.norm(acc, axis=1, keepdims=True) + 1e-12)

    # Per-corner normal: average of faces around the welded vertex whose normal is within CREASE of this face.
    cosT = np.cos(np.radians(CREASE))
    N = np.zeros_like(V)
    for k in range(3):
        vi = F[:, k]
        g = weld[vi]
        keep = (np.sum(fu * ref[g], axis=1) > cosT)
        contrib = np.where(keep[:, None], ref[g], fu)
        np.add.at(N, vi, contrib)
    N /= (np.linalg.norm(N, axis=1, keepdims=True) + 1e-12)

    # Relax: blend each welded vertex normal with its 1-ring neighbours (by weld group).
    edges = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    ge = weld[edges]
    for _ in range(RELAX):
        gn = np.zeros((G, 3))
        np.add.at(gn, weld, N)
        nb = np.zeros((G, 3))
        np.add.at(nb, ge[:, 0], gn[ge[:, 1]])
        np.add.at(nb, ge[:, 1], gn[ge[:, 0]])
        nb /= (np.linalg.norm(nb, axis=1, keepdims=True) + 1e-12)
        cand = N * 0.5 + nb[weld] * 0.5
        cand /= (np.linalg.norm(cand, axis=1, keepdims=True) + 1e-12)
        ok = np.sum(cand * N, axis=1) > cosT          # never smear across a crease
        N[ok] = cand[ok]

    with open(path, "w") as f:
        for line in head:
            if not line.startswith("f "):
                f.write(line)
        for p in V:
            f.write("v %.5f %.5f %.5f\n" % tuple(p))
        f.writelines(T)
        for n in N:
            f.write("vn %.4f %.4f %.4f\n" % tuple(n))
        for fa in F + 1:
            f.write("f %d/%d/%d %d/%d/%d %d/%d/%d\n" % (fa[0], fa[0], fa[0], fa[1], fa[1], fa[1], fa[2], fa[2], fa[2]))
    print(path, len(V), "verts", G, "welded")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        main(p)
