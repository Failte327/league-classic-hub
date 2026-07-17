"""Rebuild Data/seed/items.json from the real League Classic ("Jade") item pool.

Source: the main items.json (not jade-prefixed) — classic items share the file with
retail items, distinguished by id range 770000-779999.

Excludes 8 ids that are unfinished PBE scaffolding rather than real content (blank
descriptions / an unresolved localization-key description / dev-tool-looking price-0
items with nonsense category mixes) — see EXCLUDE_IDS below for specifics.

The existing seed's `category` field is a hand-curated taxonomy (Boots/Offense/Magic/
Support/Defense/Jungle/AttackSpeed/Mana/Vision/Consumable/Component/Other) that can't
be mechanically derived from the raw categories[] tags (e.g. "Support" spans aura
items, gp10 items, and wards; that's an editorial judgment call, not a tag lookup). So
for any of the 153 real items that match something already in the seed (by slug, or
via ALIASES for the handful of pure renames), we keep the existing category and only
correct Name/Desc/Icon. Only the items with no prior seed entry need a fresh category
assignment — NEW_ITEM_CATEGORIES below, chosen by hand to match the existing bucket's
own precedent (e.g. AttackSpeed+SpellDamage combos already live in "AttackSpeed", not
"Magic"; Consumable+Vision combos already live in "Vision", not "Consumable").
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import (ASSETS_DIR, SEED_DIR, download_icon, fetch, load_manifest,
                     record_source, save_manifest, slugify, strip_html, write_seed)

EXCLUDE_IDS = {
    773513, 773514, 773515, 773516,  # Hex Core chain: blank desc, shared leftover icon, no recipe
    772001,   # "Recall": blank desc, empty categories
    771500,   # "Penetrating Bullets": desc is a literal unresolved localization key
    772139, 772140,  # AP/AD "Rune Replacer": price 0, nonsense category mix, borrowed icons
}

# old-seed slug -> real slug, for the few items that were just renamed
ALIASES = {
    "bf-sword": "b-f-sword",
    "bloodthirster": "the-bloodthirster",
    "kages-lucky-pick": "lucky-pick",
}

NEW_ITEM_CATEGORIES = {
    "ardent-censer": "Support",
    "atmas-impaler": "Offense",
    "bag-of-tea": "Consumable",
    "candy-corn": "Consumable",
    "chain-vest": "Component",
    "eggnog": "Consumable",
    "executioners-calling": "Offense",
    "explorers-ward": "Vision",
    "guardian-angel": "Defense",
    "iceborn-gauntlet": "Defense",
    "nashors-tooth": "AttackSpeed",
    "red-trinket": "Vision",
    "seekers-armguard": "Component",
    "shusheis-mana-jug": "Magic",
    "sight-ward": "Vision",
    "spectres-cowl": "Defense",
    "stack-of-sunfire-capes": "Defense",
    "starks-fervor": "Support",
    "stinger": "AttackSpeed",
    "the-brutalizer": "Offense",
    "vampiric-scepter": "Component",
    "wits-end": "AttackSpeed",
    "yellow-trinket": "Vision",
}


def build():
    data, raw = fetch("items.json")  # the main item file; classic items share it with retail
    manifest = load_manifest()
    record_source(manifest, "items.json", raw, "items.json")
    save_manifest(manifest)

    jade = [it for it in data if 770000 <= it["id"] < 780000 and it["id"] not in EXCLUDE_IDS]

    old = json.loads((SEED_DIR / "items.json").read_text())
    old_by_slug = {o["slug"]: o for o in old}

    out = []
    unclassified = []
    for it in jade:
        slug = slugify(it["name"])
        old_match = old_by_slug.get(slug)
        if old_match is None:
            # maybe this real item is the target of an alias from an old slug
            for old_slug, new_slug in ALIASES.items():
                if new_slug == slug:
                    old_match = old_by_slug.get(old_slug)
                    break

        if old_match is not None:
            category = old_match["category"]
        elif slug in NEW_ITEM_CATEGORIES:
            category = NEW_ITEM_CATEGORIES[slug]
        else:
            category = "Other"
            unclassified.append(slug)

        out.append({
            "ddragonId": it["id"],
            "name": it["name"],
            "slug": slug,
            "category": category,
            "desc": strip_html(it["description"]),
            "icon": f"assets/items/{slug}.png",
            "_iconSource": it["iconPath"],
        })

    out.sort(key=lambda i: (i["category"], i["name"]))
    print(f"items: {len(out)} real entries (excluded {len(EXCLUDE_IDS)} unfinished)")
    if unclassified:
        print(f"WARNING: {len(unclassified)} items fell back to 'Other' with no explicit "
              f"classification: {unclassified}")
    return out


def download_icons(items):
    for it in items:
        dest = ASSETS_DIR / "items" / f"{it['slug']}.png"
        download_icon(it["_iconSource"], dest)
    print(f"downloaded {len(items)} item icons")


if __name__ == "__main__":
    items = build()
    download_icons(items)
    for it in items:
        del it["_iconSource"]
    write_seed("items.json", items)
