"""Rebuild Data/seed/abilities.json (and lightly correct champions.json) from the
real League Classic ("Jade") per-champion kits.

Source: jade-champions.json lists the 60 real classic-roster champion ids; each
champions/{id}.json has passive + spells[] (q/w/e/r), each with name/description/
abilityIconPath. Uses the plain `description` field (clean flavor text) rather than
`dynamicDescription` (has unresolved @Placeholder@ scaling tokens that would need
per-rank computation to fill in).

champions.json gets a light touch here too: Name/Title/Blurb/IsAvailable corrected
against the same 60 fetches (the real roster, vs. the old guess's 61-of-69), reusing
the existing seed's Slug/Icon/Lore untouched (full Lore text isn't in this PBE data).
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import (ASSETS_DIR, SEED_DIR, download_icon, fetch, load_manifest,
                     record_source, save_manifest, slugify, write_seed)

SLOT_ORDER = ["passive", "q", "w", "e", "r"]

# real-name-derived slug -> existing seed slug, for champions whose current official
# name (post-VGU) slugifies differently than the pre-VGU identity this seed uses.
ALIASES = {
    "nunu-willump": "nunu",
}


def fetch_champion(jade_id):
    data, raw = fetch(f"champions/{jade_id}.json")
    return data, raw


def build():
    roster, roster_raw = fetch("jade-champions.json")
    manifest = load_manifest()
    record_source(manifest, "jade-champions.json", roster_raw, "v1/jade-champions.json")

    champion_ids = [e["value"]["championId"] for e in roster[0]["mChampions"]]
    print(f"real roster: {len(champion_ids)} champions")

    abilities = []
    champ_updates = {}
    for jade_id in champion_ids:
        champ, champ_raw = fetch_champion(jade_id)
        record_source(manifest, f"champions/{jade_id}.json", champ_raw, f"v1/champions/{jade_id}.json")

        slug = slugify(champ["name"])
        slug = ALIASES.get(slug, slug)
        champ_updates[slug] = {
            "name": champ["name"],
            "title": champ["title"],
            "blurb": champ["shortBio"],
        }

        entries = [("P", champ["passive"])] + [(sp["spellKey"].upper(), sp) for sp in champ["spells"]]
        for slot, sp in entries:
            # DbSeeder's BackfillAbilityNamesAsync intentionally never rewrites an
            # existing row's IconPath — an icon correction ships by overwriting the
            # file at the same path an already-seeded DB row already points to. The
            # passive slot's filename ("passive.png") predates the Q/W/E/R convention
            # ("q.png" etc.) and has to be kept for that reason.
            filename = "passive.png" if slot == "P" else f"{slot.lower()}.png"
            abilities.append({
                "champ": slug,
                "slot": slot,
                "name": sp["name"],
                "desc": sp["description"],
                "icon": f"assets/abilities/{slug}/{filename}",
                "_iconSource": sp["abilityIconPath"],
            })

    save_manifest(manifest)
    print(f"abilities: {len(abilities)} entries across {len(champion_ids)} champions")
    return abilities, champ_updates


def download_icons(abilities):
    for a in abilities:
        dest = ASSETS_DIR / a["icon"].removeprefix("assets/")
        download_icon(a["_iconSource"], dest)
    print(f"downloaded {len(abilities)} ability icons")


def update_champions(champ_updates):
    path = SEED_DIR / "champions.json"
    champions = json.loads(path.read_text())
    real_slugs = set(champ_updates)

    for c in champions:
        if c["slug"] == "generic":
            continue
        was_available = c["available"]
        c["available"] = c["slug"] in real_slugs
        if c["slug"] in champ_updates:
            u = champ_updates[c["slug"]]
            c["name"] = u["name"]
            c["title"] = u["title"]
            if u["blurb"]:
                c["blurb"] = u["blurb"]
        if was_available != c["available"]:
            print(f"  roster change: {c['slug']} available {was_available} -> {c['available']}")

    seed_slugs = {c["slug"] for c in champions}
    missing = real_slugs - seed_slugs
    if missing:
        print(f"WARNING: real roster champions not found in champions.json seed at all: {missing}")

    path.write_text(json.dumps(champions, indent=1) + "\n")
    print(f"wrote {path} ({len(champions)} entries)")


if __name__ == "__main__":
    abilities, champ_updates = build()
    download_icons(abilities)
    for a in abilities:
        del a["_iconSource"]
    write_seed("abilities.json", abilities)
    update_champions(champ_updates)
