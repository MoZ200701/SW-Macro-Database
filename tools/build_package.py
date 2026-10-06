#!/usr/bin/env python3
"""Build the release package: dist/swfm-skills-<version>.zip.

    python3 tools/build_package.py --version 1.4.0 --out dist/
    python3 tools/build_package.py --version 1.4.0 --out dist/ --notes-from v1.3.0

The package holds two skills plus the prebuilt scripts (layout in the
repo's release docs). The zip is deterministic: sorted entries, a fixed
timestamp, fixed modes. Building the same commit twice gives byte-identical
output, so the sha256 values in package.json can be trusted.

`--notes-from <ref>` also writes dist/release-notes.md: what changed in the
scripts and in the entries since that tag. Omit it, or name a ref that does not
exist, and everything is listed as new (a first release).

Python 3 stdlib only. Fails (non-zero, message on stderr) rather than producing
a package with a broken template or a broken link inside entries/ or code/.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import pathlib
import posixpath
import re
import subprocess
import sys
import urllib.parse
import zipfile

import build_manifest

ROOT = pathlib.Path(__file__).resolve().parent.parent

SKILL = "skills/sw-macro-database"
CONTRIBUTE = "skills/sw-macro-contribute"
PLACEHOLDERS = ("INDEX", "SCRIPTS", "VERSION")
SKIP_NAMES = {".DS_Store", "__pycache__"}
SKIP_SUFFIXES = {".pyc"}
ZIP_DATE = (1980, 1, 1, 0, 0, 0)
VERSION_RE = re.compile(r"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.+-]+)?$")


class BuildError(Exception):
    """The package cannot be built; the message says why."""


def _skipped(path: pathlib.Path) -> bool:
    return (
        path.name in SKIP_NAMES
        or path.suffix in SKIP_SUFFIXES
        or any(part in SKIP_NAMES for part in path.parts)
    )


def _read(root: pathlib.Path, rel: str) -> bytes:
    path = root / rel
    if not path.is_file():
        raise BuildError(f"{rel} is missing")
    return path.read_bytes()


def _tree(root: pathlib.Path, rel_dir: str, dest: str, exclude: set[str] = frozenset()) -> dict[str, bytes]:
    """Every file under root/rel_dir, keyed by its path inside the package."""
    base = root / rel_dir
    out: dict[str, bytes] = {}
    if not base.is_dir():
        return out
    for path in sorted(base.rglob("*")):
        if not path.is_file() or _skipped(path.relative_to(base)):
            continue
        rel = path.relative_to(base).as_posix()
        if rel in exclude:
            continue
        out[f"{dest}/{rel}"] = path.read_bytes()
    return out


# --------------------------------------------------------------------------
# scripts


def load_scripts(root: pathlib.Path) -> dict[str, dict]:
    """{id: {"meta": script.json dict, "sha256": ..., "json": bytes, "csx": bytes}}.

    Reads only; the shape of script.json is tools/validate.py's job. A folder
    that cannot even be read raises BuildError.
    """
    out: dict[str, dict] = {}
    base = root / "scripts"
    if not base.is_dir():
        return out
    for folder in sorted(p for p in base.iterdir() if p.is_dir() and p.name not in SKIP_NAMES):
        raw_json = _read(root, f"scripts/{folder.name}/script.json")
        raw_csx = _read(root, f"scripts/{folder.name}/script.csx")
        try:
            meta = json.loads(raw_json.decode("utf-8"))
        except ValueError as exc:
            raise BuildError(f"scripts/{folder.name}/script.json: not valid JSON ({exc})")
        if not isinstance(meta, dict):
            raise BuildError(f"scripts/{folder.name}/script.json: must be a JSON object")
        out[folder.name] = {
            "meta": meta,
            "json": raw_json,
            "csx": raw_csx,
            # The two files, back to back, are what a consumer must reproduce.
            "sha256": hashlib.sha256(raw_json + raw_csx).hexdigest(),
        }
    return out


# --------------------------------------------------------------------------
# SKILL.md generation


def render_index(manifest: dict) -> str:
    """One line per entry, grouped by category, in manifest order."""
    groups: dict[str, list[dict]] = {}
    for e in manifest["entries"]:
        groups.setdefault(e["category"], []).append(e)
    blocks = []
    for category, entries in groups.items():
        lines = [f"### {category}", ""]
        for e in entries:
            lines.append(f"- `{e['path']}` · **{e['status']}** · {e['answers']}")
        blocks.append("\n".join(lines))
    return "\n\n".join(blocks)


def render_scripts(scripts: dict[str, dict]) -> str:
    if not scripts:
        return "No prebuilt scripts in this release."
    lines = []
    for sid in sorted(scripts):
        m = scripts[sid]["meta"]
        irreversible = ", irreversible" if m.get("irreversible") else ""
        verified = f" (verified on {m.get('verified_on')})" if m.get("status") == "verified" else ""
        lines.append(
            f"- `{sid}` · {m.get('effects')}{irreversible} · **{m.get('status')}**{verified}"
            f" · {m.get('title')} · entry `{m.get('entry')}`"
        )
    return "\n".join(lines)


def render_skill(template: str, version: str, manifest: dict, scripts: dict[str, dict]) -> str:
    """Fill the three placeholders in one pass, so substituted text is never rescanned."""
    values = {
        "INDEX": render_index(manifest),
        "SCRIPTS": render_scripts(scripts),
        "VERSION": version,
    }
    missing = [p for p in PLACEHOLDERS if "{{" + p + "}}" not in template]
    if missing:
        raise BuildError(
            f"{SKILL}/SKILL.template.md lacks placeholder(s): "
            + ", ".join("{{" + p + "}}" for p in missing)
        )
    return re.sub(r"\{\{(INDEX|SCRIPTS|VERSION)\}\}", lambda m: values[m.group(1)], template)


# --------------------------------------------------------------------------
# link check


def check_package_links(files: dict[str, bytes]) -> tuple[list[str], list[str]]:
    """Relative markdown links inside the packaged skills/sw-macro-database/**.

    Returns (errors, warnings). Inside entries/ and code/ an unresolved link is
    an error: those folders are meant to be self-contained. Elsewhere (GOTCHAS,
    API-LEDGER, INDEX) a link to a repo-root file that is not packaged is only a
    warning. Content is never rewritten.
    """
    errors: list[str] = []
    warnings: list[str] = []
    paths = set(files)
    dirs = {posixpath.dirname(p) for p in paths}
    for p in list(dirs):
        while p:
            p = posixpath.dirname(p)
            dirs.add(p)
    for path in sorted(files):
        if not (path.startswith(SKILL + "/") and path.endswith(".md")):
            continue
        # As in build_manifest.check_links: files starting with `_` are templates,
        # and their links are placeholders by design.
        if posixpath.basename(path).startswith("_"):
            continue
        rel = path[len(SKILL) + 1:]
        strict = rel.startswith(("entries/", "code/"))
        for m in build_manifest.LINK.finditer(files[path].decode("utf-8")):
            target = urllib.parse.unquote(m.group(1).strip())
            if target.startswith(("http://", "https://", "mailto:")):
                continue
            resolved = posixpath.normpath(posixpath.join(posixpath.dirname(path), target))
            inside = resolved.startswith(SKILL + "/") or resolved == SKILL
            if inside and (resolved in paths or resolved in dirs):
                continue
            msg = f"{path}: link does not resolve inside the package -> {target}"
            (errors if strict else warnings).append(msg)
    return errors, warnings


# --------------------------------------------------------------------------
# build


def git_bytes(root: pathlib.Path, *args: str) -> bytes | None:
    """Raw stdout of a git command run in root, or None if it fails.

    Bytes, not text: the script sha256 is over exact bytes, so no newline
    translation is allowed on the way in.
    """
    try:
        res = subprocess.run(["git", "-C", str(root), *args], capture_output=True, check=True)
    except (OSError, subprocess.CalledProcessError):
        return None
    return res.stdout


def git(root: pathlib.Path, *args: str) -> str | None:
    out = git_bytes(root, *args)
    return None if out is None else out.decode("utf-8", errors="replace")


def head_commit(root: pathlib.Path) -> str:
    if not (root / ".git").exists():
        return "unknown"
    out = git(root, "rev-parse", "HEAD")
    return out.strip() if out and out.strip() else "unknown"


def assemble(root: pathlib.Path, version: str) -> tuple[dict[str, bytes], list[str]]:
    """Every file in the package, keyed by path, plus link warnings. No disk writes."""
    if not VERSION_RE.match(version):
        raise BuildError(f"version {version!r} must look like 1.4.0 (no leading 'v')")

    manifest_bytes = _read(root, "manifest.json")
    manifest = json.loads(manifest_bytes.decode("utf-8"))
    scripts = load_scripts(root)
    template = _read(root, f"{SKILL}/SKILL.template.md").decode("utf-8")

    files: dict[str, bytes] = {
        f"{SKILL}/SKILL.md": render_skill(template, version, manifest, scripts).encode("utf-8"),
        f"{SKILL}/manifest.json": manifest_bytes,
        f"{SKILL}/GOTCHAS.md": _read(root, "GOTCHAS.md"),
        f"{SKILL}/API-LEDGER.md": _read(root, "API-LEDGER.md"),
        f"{SKILL}/INDEX.md": _read(root, "INDEX.md"),
        # Entries link to ../../CONTRIBUTING.md (the authoring rules); packaging a
        # copy at the skill root keeps those links resolving without rewriting them.
        f"{SKILL}/CONTRIBUTING.md": _read(root, "CONTRIBUTING.md"),
        f"{CONTRIBUTE}/SKILL.md": _read(root, f"{CONTRIBUTE}/SKILL.md"),
        f"{CONTRIBUTE}/CONTRIBUTING.md": _read(root, "CONTRIBUTING.md"),
        f"{CONTRIBUTE}/_TEMPLATE.md": _read(root, "entries/_TEMPLATE.md"),
    }
    files.update(_tree(root, "entries", f"{SKILL}/entries"))
    files.update(_tree(root, "code", f"{SKILL}/code"))
    for sid, s in scripts.items():
        files[f"scripts/{sid}/script.json"] = s["json"]
        files[f"scripts/{sid}/script.csx"] = s["csx"]

    errors, warnings = check_package_links(files)
    if errors:
        raise BuildError("broken links in the package:\n  " + "\n  ".join(errors))

    package_json = {
        "schema": 1,
        "version": version,
        "builtFrom": head_commit(root),
        "files": [
            {"path": p, "sha256": hashlib.sha256(files[p]).hexdigest()} for p in sorted(files)
        ],
        "scripts": [{"id": sid, "sha256": scripts[sid]["sha256"]} for sid in sorted(scripts)],
    }
    files["package.json"] = (json.dumps(package_json, indent=2) + "\n").encode("utf-8")
    return files, warnings


def write_zip(files: dict[str, bytes], dest: pathlib.Path) -> None:
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as zf:
        for path in sorted(files):
            info = zipfile.ZipInfo(path, date_time=ZIP_DATE)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3  # unix, whatever the build machine is
            info.external_attr = 0o644 << 16
            zf.writestr(info, files[path])
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(buf.getvalue())


def build(root: pathlib.Path, version: str, out_dir: pathlib.Path) -> tuple[pathlib.Path, list[str]]:
    """Build the zip. Returns (zip path, warnings); raises BuildError on failure."""
    files, warnings = assemble(root, version)
    dest = out_dir / f"swfm-skills-{version}.zip"
    write_zip(files, dest)
    return dest, warnings


# --------------------------------------------------------------------------
# release notes


def _at_ref(root: pathlib.Path, ref: str | None):
    """(manifest dict, {script id: (meta, sha)}) at a git ref; (None, None) if no such ref."""
    if not ref:
        return None, None
    raw = git_bytes(root, "show", f"{ref}:manifest.json")
    if raw is None:
        return None, None
    manifest = json.loads(raw)
    scripts: dict[str, tuple[dict, str]] = {}
    listing = git(root, "ls-tree", "-r", "--name-only", ref, "scripts/") or ""
    ids = sorted({p.split("/")[1] for p in listing.splitlines() if p.count("/") >= 2})
    for sid in ids:
        j = git_bytes(root, "show", f"{ref}:scripts/{sid}/script.json")
        c = git_bytes(root, "show", f"{ref}:scripts/{sid}/script.csx")
        if j is None or c is None:
            continue
        try:
            meta = json.loads(j)
        except ValueError:
            meta = {}
        scripts[sid] = (meta, hashlib.sha256(j + c).hexdigest())
    return manifest, scripts


def release_notes(root: pathlib.Path, ref: str | None) -> str:
    old_manifest, old_scripts = _at_ref(root, ref)
    if old_manifest is None:
        old_manifest, old_scripts = {"entries": []}, {}
    new_manifest = json.loads(_read(root, "manifest.json").decode("utf-8"))
    new_scripts = {
        sid: (s["meta"], s["sha256"]) for sid, s in load_scripts(root).items()
    }

    def desc(meta: dict) -> str:
        return f"{meta.get('effects')}, {meta.get('status')}"

    lines: list[str] = []

    script_lines = []
    for sid in sorted(set(old_scripts) | set(new_scripts)):
        if sid not in old_scripts:
            script_lines.append(f"- added `{sid}` ({desc(new_scripts[sid][0])})")
        elif sid not in new_scripts:
            script_lines.append(f"- removed `{sid}`")
        elif old_scripts[sid][1] != new_scripts[sid][1]:
            script_lines.append(f"- changed `{sid}` ({desc(new_scripts[sid][0])})")
    if script_lines:  # the heading appears only when something changed
        lines += ["## Scripts changed", "", *script_lines, ""]

    old_e = {e["id"]: e for e in old_manifest["entries"]}
    new_e = {e["id"]: e for e in new_manifest["entries"]}
    added = [new_e[i] for i in new_e if i not in old_e]
    removed = [old_e[i] for i in old_e if i not in new_e]
    changed = [(i, old_e[i]["status"], new_e[i]["status"])
               for i in new_e if i in old_e and old_e[i]["status"] != new_e[i]["status"]]

    lines += ["## Entries", ""]
    if not (added or removed or changed):
        lines.append("No changes.")
    if added:
        lines.append(f"- {len(added)} new:")
        lines += [f"  - {e['title']} ({e['status']})" for e in added]
    if changed:
        lines.append("- Status changes:")
        lines += [f"  - `{i}`: {a} → {b}" for i, a, b in changed]
    if removed:
        lines.append("- Removed:")
        lines += [f"  - {e['title']}" for e in removed]
    return "\n".join(lines) + "\n"


# --------------------------------------------------------------------------


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--version", required=True, help="e.g. 1.4.0 (no leading v)")
    ap.add_argument("--out", default="dist", help="output directory (default: dist)")
    ap.add_argument("--notes-from", metavar="REF",
                    help="previous git tag; also write release-notes.md against it")
    ap.add_argument("--root", default=str(ROOT), help="repo root (default: this repo)")
    args = ap.parse_args(argv)

    root = pathlib.Path(args.root).resolve()
    out = pathlib.Path(args.out)
    try:
        dest, warnings = build(root, args.version, out)
        for w in warnings:
            print(f"warning: {w}", file=sys.stderr)
        print(f"{dest} ({dest.stat().st_size} bytes)")
        if args.notes_from is not None:
            notes = out / "release-notes.md"
            notes.write_text(release_notes(root, args.notes_from or None), encoding="utf-8")
            print(notes)
    except BuildError as exc:
        print(f"package not built: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
