#!/usr/bin/env python3
"""Check project package declarations against the installed Unity Editor.

This read-only check detects missing built-in modules and incomplete lockfile
graphs. It does not resolve/download packages or claim Unity import succeeded.
"""
from pathlib import Path
from collections import deque
import argparse
import json
import re
import sys


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--editor-root", type=Path,
                    default=Path("/workspace/.tools/unity/6000.6.3f1/Editor"))
options = parser.parse_args()
root = Path(__file__).resolve().parents[2]
builtin_root = options.editor_root.resolve() / "Data/Resources/PackageManager/BuiltInPackages"
if not builtin_root.is_dir():
    parser.error(f"Installed Editor package metadata is missing: {builtin_root}")


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError) as error:
        parser.error(f"Cannot read {path}: {error}")


manifest = read_json(root / "Packages/manifest.json")
declared = manifest["dependencies"]
locked = read_json(root / "Packages/packages-lock.json")["dependencies"]
builtins = {}
for path in sorted(builtin_root.glob("*/package.json")):
    package = read_json(path)
    builtins[package["name"]] = package

errors = []
warnings = []
notices = []


def numeric_version(value):
    return tuple(map(int, value.split("."))) if re.fullmatch(r"\d+\.\d+\.\d+", value) else None


for name, version in declared.items():
    if name not in locked:
        errors.append(f"Manifest dependency has no lock entry: {name}")
    elif locked[name]["version"] != version:
        errors.append(f"Manifest/lock version differs for {name}: {version} / {locked[name]['version']}")

for name, entry in locked.items():
    if (name.startswith("com.unity.modules.") or entry.get("source") == "builtin") and name not in builtins:
        errors.append(f"Built-in package is absent from this Editor: {name}")
    for dependency, required in entry.get("dependencies", {}).items():
        if dependency not in locked:
            errors.append(f"Dangling lockfile dependency: {name} -> {dependency}")
            continue
        minimum = numeric_version(required)
        selected = numeric_version(locked[dependency]["version"])
        if minimum is not None and selected is not None and selected < minimum:
            errors.append(f"Unsatisfied dependency: {name} needs {dependency} >= {required}, "
                          f"locked {locked[dependency]['version']}")
    builtin = builtins.get(name)
    if builtin and entry.get("source") == "builtin" and entry["version"] != builtin["version"]:
        warnings.append(f"{name}: locked {entry['version']}, Editor built-in {builtin['version']}; "
                        "requires Package Manager resolution and compatibility review")
    if builtin and entry.get("source") == "builtin" and entry.get("dependencies", {}) != builtin.get("dependencies", {}):
        warnings.append(f"Built-in dependency metadata differs from the installed Editor: {name}")
    if builtin and entry.get("source") == "registry" and entry["version"] != builtin["version"]:
        notices.append(f"{name}: registry pin {entry['version']} retained; Editor also offers "
                       f"built-in {builtin['version']} (separate compatibility review before migration)")
    if entry.get("source") == "git" and not entry.get("hash"):
        errors.append(f"Git package has no locked revision: {name}")

depths = {}
pending = deque((name, 0) for name in declared)
while pending:
    name, depth = pending.popleft()
    if name in depths or name not in locked:
        continue
    depths[name] = depth
    if locked[name].get("depth") != depth:
        errors.append(f"Incorrect lock depth for {name}: {locked[name].get('depth')}, expected {depth}")
    pending.extend((dependency, depth + 1) for dependency in locked[name].get("dependencies", {}))
for name in sorted(locked.keys() - depths.keys()):
    warnings.append(f"Lock entry is unreachable from the manifest: {name}")

for registry in manifest.get("scopedRegistries", []):
    scopes = registry.get("scopes", [])
    if not any(name == scope or name.startswith(scope + ".") for name in locked for scope in scopes):
        warnings.append(f"Scoped registry has no matching dependency: {registry['name']} ({', '.join(scopes)})")

for notice in notices:
    print("INFO:", notice)
for warning in warnings:
    print("WARN:", warning)
for error in errors:
    print("ERROR:", error)
print(f"{'FAIL' if errors else 'PASS'}: {len(declared)} manifest dependencies / {len(locked)} lock entries; "
      f"{len(errors)} errors, {len(warnings)} unresolved warnings.")
print("This is metadata validation; Unity package resolution, import and player build remain separate checks.")
sys.exit(1 if errors else 0)
