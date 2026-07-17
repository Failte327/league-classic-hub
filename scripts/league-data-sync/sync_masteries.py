"""Rebuild Data/seed/masteries.json from the real League Classic ("Jade") mastery trees.

Source: jade-mastery-display.json — trees[].rows[].masteries[], row = index in
rows[] (0-based; stored 1-based to match the existing Row convention), col = index
in masteries[] (null = empty slot). No per-mastery prereq field: the real tree only
gates by row point-threshold (4 points per row), which guide-editor.js's
ptsAbove(...) >= 4 * (row - 1) already implements — so every mastery here gets
prereq = null.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import (ASSETS_DIR, download_icon, fetch, load_manifest,
                     record_source, save_manifest, write_seed)


def build():
    data, raw = fetch("jade-mastery-display.json")
    manifest = load_manifest()
    record_source(manifest, "jade-mastery-display.json", raw, "v1/jade-mastery-display.json")
    save_manifest(manifest)

    out = []
    for tree in data["trees"]:
        for row_index, row in enumerate(tree["rows"]):
            for col_index, m in enumerate(row["masteries"]):
                if m is None:
                    continue
                out.append({
                    "id": m["id"],
                    "name": m["name"],
                    "tree": tree["name"],
                    "row": row_index + 1,
                    "col": col_index,
                    "ranks": m["maxRank"],
                    "prereq": None,
                    "desc": m["description"],
                    "icon": f"assets/masteries/{m['id']}.png",
                    "_iconSource": m["activeIconPath"],
                })
    out.sort(key=lambda m: (m["tree"], m["row"], m["col"]))
    print(f"masteries: {len(out)} nodes")
    return out


def download_icons(masteries):
    for m in masteries:
        dest = ASSETS_DIR / "masteries" / f"{m['id']}.png"
        download_icon(m["_iconSource"], dest)
    print(f"downloaded {len(masteries)} mastery icons")


if __name__ == "__main__":
    masteries = build()
    download_icons(masteries)
    for m in masteries:
        del m["_iconSource"]
    write_seed("masteries.json", masteries)
