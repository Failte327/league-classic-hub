"""Shared helpers for the League Classic data-sync scripts.

These scripts pull League Classic's real data (Riot's internal codename: "Jade")
from CommunityDragon's mirror and rebuild Data/seed/*.json + wwwroot/assets/*.
Today that mirror is the PBE branch, since League Classic hasn't shipped to live
servers yet; BASE_URL is the one thing to change once it does (presumably to a
real ddragon.leagueoflegends.com patch once Riot publishes rune.json/mastery.json/
item.json for it again).

Re-run any sync_*.py script at any time: it re-fetches, recomputes each source
file's hash, and updates source-manifest.json. Compare against a prior commit of
that manifest to see whether upstream data has actually changed before re-running
the (slower, network-heavy) full rebuild.
"""
import hashlib
import json
import re
import time
import urllib.request
from pathlib import Path

BASE_URL = "https://raw.communitydragon.org/pbe/plugins/rcp-be-lol-game-data/global/default"
SOURCE_BRANCH = "pbe"  # switch to the live patch identifier once League Classic ships

REPO_ROOT = Path(__file__).resolve().parents[2]
SEED_DIR = REPO_ROOT / "src/LeagueClassic.Web/Data/seed"
ASSETS_DIR = REPO_ROOT / "src/LeagueClassic.Web/wwwroot/assets"
MANIFEST_PATH = SEED_DIR / "source-manifest.json"


HEADERS = {"User-Agent": "curl/8.5.0"}


def _get(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=30) as resp:
        return resp.read()


def fetch(path):
    """GET a v1 data file (e.g. 'jade-perks.json' or 'items.json') and return parsed JSON."""
    raw = _get(f"{BASE_URL}/v1/{path}")
    return json.loads(raw), raw


def icon_url(icon_path):
    """Map a raw iconPath/abilityIconPath field to its downloadable CDragon URL."""
    prefix = "/lol-game-data/assets"
    lower = icon_path.lower()
    assert lower.startswith(prefix), f"unexpected icon path shape: {icon_path}"
    return BASE_URL + lower[len(prefix):]


def download_icon(icon_path, dest: Path):
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(_get(icon_url(icon_path)))


def slugify(name):
    s = name.lower().replace("'", "")
    s = re.sub(r"[^a-z0-9]+", "-", s)
    return s.strip("-")


def strip_html(desc):
    if not desc:
        return None
    text = re.sub(r"<br\s*/?>", "\n", desc)
    text = re.sub(r"<[^>]+>", "", text)
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    text = "\n".join(line.strip() for line in text.split("\n"))
    return text.strip()


def load_manifest():
    if MANIFEST_PATH.exists():
        return json.loads(MANIFEST_PATH.read_text())
    return {"branch": SOURCE_BRANCH, "baseUrl": BASE_URL, "sources": {}}


def record_source(manifest, name, raw_bytes, url):
    manifest.setdefault("sources", {})[name] = {
        "url": url,
        "sha256": hashlib.sha256(raw_bytes).hexdigest(),
        "fetchedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }


def save_manifest(manifest):
    manifest["branch"] = SOURCE_BRANCH
    manifest["baseUrl"] = BASE_URL
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n")


def write_seed(filename, data):
    path = SEED_DIR / filename
    path.write_text(json.dumps(data, indent=1) + "\n")
    print(f"wrote {path} ({len(data)} entries)")
