#!/usr/bin/env python3
"""Rebuild manifest.json from every entry's frontmatter.

Run from the repo root after adding or changing an entry:

    python3 tools/build_manifest.py

Exits non-zero and prints the problems if any entry is malformed. That is the
one automated quality gate this repo has, so it is deliberately strict.
"""

from __future__ import annotations

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
ENTRIES = ROOT / "entries"

REQUIRED = ["id", "title", "status", "language", "api", "keywords", "answers"]
# `api` may legitimately be empty: an entry about a file format or a convention
# teaches no member. It must still be present, so its absence is a decision.
MAY_BE_EMPTY = {"api"}
STATUSES = {"verified", "partly-verified", "unverified", "superseded"}

LIST_FIELDS = {"language", "api", "keywords"}


def parse_frontmatter(text: str):
    """The block between the first two `---` lines, as a dict.

    A deliberately small parser: scalars, and `[a, b]` inline lists. Anything
    fancier is a sign the frontmatter is doing too much.
    """
    if not text.startswith("---\n"):
        return None
    end = text.find("\n---\n", 4)
    if end < 0:
        return None
    out = {}
    for line in text[4:end].split("\n"):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        m = re.match(r"^([A-Za-z_]+):\s*(.*)$", line)
        if not m:
            continue
        key, raw = m.group(1), m.group(2).strip()
        if raw.startswith("[") and raw.endswith("]"):
            inner = raw[1:-1].strip()
            out[key] = [v.strip().strip('"\'') for v in inner.split(",") if v.strip()]
        elif raw in ("null", "~", ""):
            out[key] = None
        else:
            out[key] = raw.strip('"\'')
    return out


LINK = re.compile(r"\[[^\]]*\]\(([^)#]+?)(?:#[^)]*)?\)")


def check_links() -> list[str]:
    """Every relative markdown link in the repo must resolve.

    Files whose name starts with `_` are skipped: the template's links are
    placeholders by design.
    """
    bad = []
    for md in ROOT.rglob("*.md"):
        if ".git" in md.parts or md.name.startswith("_"):
            continue
        for m in LINK.finditer(md.read_text(encoding="utf-8")):
            target = m.group(1)
            if target.startswith(("http://", "https://", "mailto:")):
                continue
            if not (md.parent / target).resolve().exists():
                bad.append(f"{md.relative_to(ROOT).as_posix()}: broken link -> {target}")
    return bad


def main() -> int:
    problems: list[str] = []
    seen_ids: dict[str, str] = {}
    records = []

    paths = sorted(p for p in ENTRIES.rglob("*.md") if not p.name.startswith("_"))
    if not paths:
        print("no entries found", file=sys.stderr)
        return 1

    for path in paths:
        rel = path.relative_to(ROOT).as_posix()
        fm = parse_frontmatter(path.read_text(encoding="utf-8"))
        if fm is None:
            problems.append(f"{rel}: no frontmatter block")
            continue

        for field in REQUIRED:
            if field not in fm:
                problems.append(f"{rel}: missing field '{field}'")
            elif fm[field] in (None, "", []) and field not in MAY_BE_EMPTY:
                problems.append(f"{rel}: empty field '{field}'")

        status = fm.get("status")
        if status not in STATUSES:
            problems.append(f"{rel}: status {status!r} is not one of {sorted(STATUSES)}")
        if status == "verified" and not fm.get("verified_on"):
            problems.append(f"{rel}: status 'verified' requires verified_on naming the version")

        for field in LIST_FIELDS:
            if field in fm and not isinstance(fm[field], list):
                problems.append(f"{rel}: '{field}' must be an inline list, e.g. [a, b]")

        entry_id = fm.get("id")
        if entry_id:
            if entry_id in seen_ids:
                problems.append(f"{rel}: duplicate id '{entry_id}', also in {seen_ids[entry_id]}")
            seen_ids[entry_id] = rel

        answers = fm.get("answers") or ""
        if answers and not answers.rstrip().endswith("?"):
            problems.append(f"{rel}: 'answers' should be a question ending in '?'")

        records.append({
            "id": entry_id,
            "title": fm.get("title"),
            "path": rel,
            "category": path.parent.name,
            "status": status,
            "verified_on": fm.get("verified_on"),
            "language": fm.get("language", []),
            "api": fm.get("api", []),
            "keywords": fm.get("keywords", []),
            "answers": answers,
        })

    problems.extend(check_links())

    if problems:
        print("manifest not written; fix these first:\n", file=sys.stderr)
        for p in problems:
            print(f"  {p}", file=sys.stderr)
        return 1

    by_status: dict[str, int] = {}
    for r in records:
        by_status[r["status"]] = by_status.get(r["status"], 0) + 1

    manifest = {
        "schema": 1,
        "description": (
            "SolidWorks automation entries. Each answers one question. Check "
            "'status' before trusting an entry: verified means it ran against "
            "the named version; unverified means it is well-formed against the "
            "documented API but was never executed."
        ),
        "count": len(records),
        "by_status": dict(sorted(by_status.items())),
        "entries": records,
    }

    (ROOT / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"manifest.json: {len(records)} entries, {dict(sorted(by_status.items()))}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
