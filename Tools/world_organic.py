"""
Adds organic vertex jitter to world.json.
Moves shared vertices off the 120-unit grid by +-SCALE units,
making room borders irregular while keeping all polygons convex.
Run from the MyriaWorld directory:
    python Tools/world_organic.py
"""

import json, math, sys
from collections import defaultdict
from pathlib import Path

SRC   = Path("Content/world.json")
DST   = Path("Content/world.json")
SCALE = 28   # max offset per axis (safe for 120-unit quad rooms)

# ── deterministic noise ───────────────────────────────────────────────────────

def frac(x):
    return x - math.floor(x)

def noise1(seed):
    return frac(math.sin(seed * 127.1) * 43758.5453)

def jitter(vi, factor=1.0):
    dx = (noise1(vi * 73.13)         - 0.5) * 2 * SCALE * factor
    dz = (noise1(vi * 137.31 + 9999) - 0.5) * 2 * SCALE * factor
    return round(dx), round(dz)

# ── convexity check ───────────────────────────────────────────────────────────

def is_convex(pts):
    n = len(pts)
    sign = None
    for i in range(n):
        ax, az = pts[i]
        bx, bz = pts[(i+1) % n]
        cx, cz = pts[(i+2) % n]
        cross = (bx - ax) * (cz - az) - (bz - az) * (cx - ax)
        if abs(cross) < 0.01:
            continue
        s = 1 if cross > 0 else -1
        if sign is None:
            sign = s
        elif s != sign:
            return False
    return True

# ── load ──────────────────────────────────────────────────────────────────────

world    = json.loads(SRC.read_text(encoding="utf-8"))
vertices = [list(v) for v in world["vertices"]]
faces    = world["faces"]

# ── classify vertices ─────────────────────────────────────────────────────────

ref_count = defaultdict(int)
for face in faces:
    for vi in face["v"]:
        ref_count[vi] += 1

# Vertices shared by 2+ faces define inter-room borders — moving them gives
# organic room shapes.  Singletons are outer-boundary tips; leave them fixed.
shared = {vi for vi in range(len(vertices)) if ref_count[vi] >= 2}
print(f"Vertices {len(vertices)}, moveable shared: {len(shared)}")

# ── apply jitter at decreasing scale until all faces stay convex ──────────────

new_verts = [list(v) for v in vertices]

for factor in [1.0, 0.75, 0.5, 0.25]:
    for vi in shared:
        dx, dz = jitter(vi, factor)
        new_verts[vi][0] = vertices[vi][0] + dx
        new_verts[vi][1] = vertices[vi][1] + dz

    bad = [face.get("roomName", "?")
           for face in faces
           if not is_convex([new_verts[vi] for vi in face["v"]])]

    if not bad:
        print(f"All convex at factor {factor:.2f}  (~+-{round(SCALE*factor)} units per axis)")
        break
    print(f"  {len(bad)} non-convex at {factor:.2f}: {bad[:5]} ...")
else:
    print("Could not achieve convexity -- aborting")
    sys.exit(1)

# ── write ─────────────────────────────────────────────────────────────────────

lines = ['{\n  "vertices": [']
for i, v in enumerate(new_verts):
    comma = "," if i < len(new_verts) - 1 else ""
    lines.append(f"    [{int(v[0]):6}, {int(v[1]):6}]{comma}")
lines.append('  ],\n  "faces": [')
for i, face in enumerate(faces):
    v_str  = ", ".join(str(x) for x in face["v"])
    rid    = face.get("roomId",   "null")
    rname  = face.get("roomName", "")
    terr   = face.get("terrain",  "grass")
    comma  = "," if i < len(faces) - 1 else ""
    lines.append(
        f'    {{ "v": [{v_str:22s}], "roomId": {rid:3}, '
        f'"roomName": "{rname:<30}", "terrain": "{terr}" }}{comma}'
    )
lines.append("  ]\n}")

DST.write_text("\n".join(lines), encoding="utf-8")
print(f"Saved -> {DST}")
