"""Rebuild Data/seed/runes.json from the real League Classic ("Jade") rune data.

Source: jade-perks.json — the 4 classic rune slots (kMark/kSeal/kGlyph/kQuintessence).
Drops entries still titled "Empty Rune" — unlocalized/unfinished PBE placeholders.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import (ASSETS_DIR, download_icon, fetch, load_manifest,
                     record_source, save_manifest, slugify, write_seed)

SLOT_MAP = {"kMark": "mark", "kSeal": "seal", "kGlyph": "glyph", "kQuintessence": "quintessence"}


def build():
    data, raw = fetch("jade-perks.json")
    manifest = load_manifest()
    record_source(manifest, "jade-perks.json", raw, "v1/jade-perks.json")
    save_manifest(manifest)

    out = []
    skipped = 0
    for p in data:
        if p["title"] == "Empty Rune":
            skipped += 1
            continue
        slug = slugify(p["title"])
        out.append({
            "ddragonId": p["id"],
            "name": p["title"],
            "slug": slug,
            "slot": SLOT_MAP[p["type"]],
            "desc": p["tooltip"].lower(),
            "icon": f"assets/runes/{slug}.png",
            "_iconSource": p["iconPath"],
        })
    out.sort(key=lambda r: (r["slot"], r["name"]))
    print(f"runes: kept {len(out)}, skipped {skipped} unfinished placeholders")
    return out


def download_icons(runes):
    for r in runes:
        dest = ASSETS_DIR / "runes" / f"{r['slug']}.png"
        download_icon(r["_iconSource"], dest)
    print(f"downloaded {len(runes)} rune icons")


if __name__ == "__main__":
    runes = build()
    download_icons(runes)
    for r in runes:
        del r["_iconSource"]
    write_seed("runes.json", runes)
