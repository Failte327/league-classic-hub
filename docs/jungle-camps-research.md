# League Classic jungle camps — research notes

Status: research only. The Resources-tab map + guide jungle-path-builder feature built from
this research was rolled back (2026-07-20) — the implementation didn't come out right and
will be attempted again later with tighter requirements. This document exists so the
underlying research doesn't have to be redone. Nothing here depends on the reverted code.

## How we got here

League Classic ("Jade" is Riot's internal codename) recreates Season 3-era League with some
S3→S4 mechanics mixed in. It hit PBE 2026-07 and ships live 2026-07-29. The site's existing
data-sync pipeline (`scripts/league-data-sync/`) pulls the curated `jade-*.json` files from
CommunityDragon's PBE mirror for runes/masteries/items/champions — but that plugin does **not**
include jungle camps, objectives, or spawn timers at all. Those live one directory over, in
CDragon's raw game-asset mirror, which is not something the existing sync scripts touch.

Base URL for everything below: `https://raw.communitydragon.org/pbe/game/`
(browsable directory listing via `https://raw.communitydragon.org/json/pbe/game/...`).

## Confirmed: camp identity and composition

The owner has played PBE matches directly and confirmed League Classic uses the
**pre-2014-jungle-rework** camp set (Wraiths / Golems / Lizard Elder), not the modern
Krug/Gromp/Murkwolf/Razorbeak set. This was independently cross-validated by data-mining: CDragon
has a set of `s3_`-prefixed character files (`data/characters/s3_baron`, `s3_dragon`,
`s3_ancientgolem`, `s3_lesserwraith`, `s3_lizardelder`) that exist **alongside** the modern
`sru_*` files under the same game build — i.e. Riot forked off old-stat versions specifically
because the modern game still uses the same names/ids for Baron and Dragon. Golem/Lizard/Wraith/
Wolf camps didn't need a fork since the modern game abandoned those names entirely after the
rework, so their original assets (bare names, no `s3_` prefix) just sat there unused until Jade
reactivated them.

Confirmed camp-to-modern-pit mapping (from the owner's own playtesting):

| Old camp | Modern pit it now occupies |
|---|---|
| Wolves (unchanged) | Wolves pit (same as always — no rework happened here) |
| Wraiths (Lesser Wraiths + 1 regular Wraith) | **Raptors** pit |
| Greater Wraith (solo) | **Gromp** pit |
| Golems (1 small Golem + 1 regular Golem, non-buff) | **Krug** pit |
| Ancient Golem (Blue Buff) + 2 companions | Traditional Blue Buff pit, both jungle halves |
| Lizard Elder (Red Buff) + 2 companions | Traditional Red Buff pit, both jungle halves |

Also confirmed via Riot's own Jade in-client UI config (`gameplay.jadeneutraltimerviewcontroller.bin`
at the CDragon path above): only **one generic Dragon** (no elemental drakes — Cloud/Ocean/
Mountain/Infernal/Elder don't apply to Jade) and **Baron Nashor**. The owner separately confirmed
via a Riot dev update that Baron's buff was reverted to its historically weakest version.

One loose end resolved during research: the "Greater Wraith" stat file (`data/characters/
greatwraith`) has an odd in-game display-name key that resolves to **"Wight"** rather than
"Greater Wraith" in the current stringtable — initially flagged as possibly a dead/unused
legacy asset. The owner's in-game observation (a solo big Wraith exists at the Gromp pit)
confirms this *is* the right stat file; the display-name string is just seemingly unfinished/
unlocalized on this PBE build, not a sign of the wrong data.

## Confirmed: base combat/reward stats

Pulled directly from each unit's `CharacterRecord` in its `.bin.json` file
(`data/characters/<name>/<name>.bin.json`). These are solid — not from static config that might
be shared/overridden per game mode, but from a dedicated per-unit stat block.

| Unit | Source file | HP | Damage | Armor | MR | Gold | XP |
|---|---|---|---|---|---|---|---|
| Giant Wolf | `giantwolf` | 1100 | 35 | 9 | — | 30 | 110 |
| Wraith (regular, Raptors pit) | `wraith` | 1000 | 35 | 15 | — | 30 | 100 |
| Lesser Wraith (small add) | `s3_lesserwraith` | 150 | — | 5 | — | 5 | 15 |
| Greater Wraith (solo, Gromp pit) | `greatwraith` | 1400 | 60 | 15 | — | 65 | 170 |
| Golem (small, Krug pit) / regular Golem | `golem` | 1200 | 59 | 12 | -10 | 55 | 135 |
| Ancient Golem (Blue Buff) | `s3_ancientgolem` | 1400 | 60 | 20 | — | 55 | 260 |
| Lizard Elder (Red Buff) | `s3_lizardelder` | 1400 | 65 | 20 | — | 55 | 260 |
| Companion Lizard (Red Buff add) | `younglizard` | 400 | — | 8 | — | 10 | 20 |
| Dragon | `s3_dragon` | 3500 | 145 | 21 | 30 | *unconfirmed* | *unconfirmed* |
| Baron Nashor | `s3_baron` | 8800 | 460 | 120 | 70 | *unconfirmed* | 800 |

Dragon/Baron gold (and Dragon's XP) show as `0`/blank in the raw `CharacterRecord` — almost
certainly because epic-monster team bounties are paid through a separate reward system, not
this per-unit field. Don't treat those as real "0" values; they're just not visible from this
data source.

## Not available anywhere — checked, confirmed absent

Exact spawn/respawn timers and Blue/Red Buff duration are **not published anywhere**, as of
2026-07-20:

- Not in Riot's own CDragon-mirrored game files. The shared map script (`data/maps/shipping/
  map11/map11.bin.json` — same file live SR uses, no separate Jade map id exists) has
  `DragonSpawnRate`/`ElderDragonSpawnRate`/`bossSpawnTime`-style variables, but they're
  wrapped in a visual-scripting graph (`ScriptSequence`/`SetVarInTableBlock` nodes) that only
  references **modern** camp names (Krug, Gromp, Murkwolf, Razorbeak, SRU_Baron, SRU_Dragon) —
  the `s3_*` names never appear in it, so whatever values are in there apply to live SR, not
  provably to Jade. The mechanism that swaps in the old monsters for Jade isn't in any file
  CDragon mirrors.
- Not in the Jade-specific UI config files (`gameplay.jadeenemyrespawntimers.bin`,
  `gameplay.jadeneutraltimerviewcontroller.bin`) — those are pure widget/layout bindings, no
  hardcoded seconds values. Tried resolving the specific localized respawn-tooltip strings
  they reference (`..._dragonrespawnjade`, `..._baronrespawnjade`) directly in the live
  stringtable — those keys don't exist yet on this PBE build.
- Not on Riot's PBE feedback boards (found a relevant thread title but couldn't fetch it).
- Not on the only rival fan site found, `leagueclassic.wiki` — it has a dedicated
  `/map-gameplay/jungle-camps-classic/` page, but it contains zero actual numbers and cites no
  source, just general statements like "faster respawn cycles."
- Not in general web search — coverage repeats "camps respawn faster than live" without
  figures, and explicitly flags some mechanics (e.g. whether level 3 needs 3 or 4 camps) as
  still unconfirmed/still being tested.

**Conclusion**: this data can only come from someone actually timing it in a live PBE match —
noting the game-clock time a camp dies and when it respawns, and the buff-icon countdown for
Blue/Red Buff duration. The owner is doing this and will hand over a `.txt` log later.

## When we pick this back up

The stats/composition table above is solid and reusable as-is. What's still missing before a
resources page + guide path-builder feature is worth rebuilding:
1. The owner's PBE timing log (spawn/respawn/buff-duration numbers).
2. A clearer spec on what "didn't come out right" about the last attempt, so the next build
   avoids repeating it.
