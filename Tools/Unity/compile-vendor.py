#!/usr/bin/env python3
"""Compile vendored assemblies against installed Unity DLLs, without running Unity.

This is a C# API compatibility check, not a Unity import or player build.
Sources are staged in a temporary directory; no Unity DLLs are redistributed.
"""
from pathlib import Path
import argparse
import json
import subprocess
import sys
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--editor-root", type=Path, default=Path("/workspace/.tools/unity/6000.6.3f1/Editor"))
parser.add_argument("--dotnet", default="/workspace/.tools/dotnet/dotnet")
parser.add_argument("--output", type=Path)
parser.add_argument("--platform", choices=("editor", "standalone", "android"), default="editor",
                    help="Check Editor or runtime-only platform branches (not a player build).")
options = parser.parse_args()
root = Path(__file__).resolve().parents[2]
data = options.editor_root.resolve() / "Data"
out = options.output.resolve() if options.output else Path(tempfile.mkdtemp(prefix="master-duel-unity-compile-"))
out.mkdir(parents=True, exist_ok=True)
dotnet = options.dotnet
compilers = sorted((data / "DotNetSdk/sdk").glob("*/Roslyn/bincore/csc.dll"))
if len(compilers) != 1:
    parser.error("Expected the installed Editor's bundled C# compiler.")
csc = str(compilers[0])
cache = data / "Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.urp-blank-17.2.1/ScriptAssemblies"
if not (cache / "UnityEngine.UI.dll").is_file():
    parser.error("The target Editor's URP template DLL cache is missing.")
framework_refs = [data / "NetStandard/ref/2.1.0/netstandard.dll"]
framework_refs += sorted((data / "NetStandard/compat/2.1.0/shims/netfx").glob("*.dll"))
engine_refs = sorted((data / "Managed/UnityEngine").glob("*.dll"))
if options.platform != "editor":
    engine_refs = [path for path in engine_refs if not path.name.startswith("UnityEditor")]
package_refs = {path.stem: path for path in cache.glob("*.dll")
                if not path.name.startswith("Assembly-CSharp")}
print("Compiler:", csc, flush=True)
print("Logs and staged compilation outputs:", out, flush=True)
definitions = {}
for path in sorted((root / "Assets/Tools").rglob("*.asmdef")):
    definition = json.loads(path.read_text(encoding="utf-8-sig"))
    editor_only = definition.get("includePlatforms") == ["Editor"]
    if options.platform != "editor" and editor_only:
        continue
    definitions[definition["name"]] = (path, definition)
# Compile each assembly with its declared project/package references only. Giving
# every assembly every package DLL hides missing asmdef references in Unity.
groups = []
pending = dict(definitions)
while pending:
    ready = [name for name, (_, definition) in pending.items()
             if not any(reference in pending for reference in definition.get("references", []))]
    if not ready:
        parser.error("Cyclic vendored assembly references: " + ", ".join(sorted(pending)))
    for name in ready:
        groups.append((name, *pending.pop(name)))
flags = [
    "UNITY_5_3_OR_NEWER", "UNITY_5_4_OR_NEWER", "UNITY_5_5_OR_NEWER", "UNITY_5_6_OR_NEWER",
]
if options.platform == "editor":
    flags += ["UNITY_EDITOR", "UNITY_EDITOR_LINUX", "UNITY_STANDALONE", "UNITY_STANDALONE_LINUX"]
elif options.platform == "android":
    flags += ["UNITY_ANDROID"]
else:
    flags += ["UNITY_STANDALONE", "UNITY_STANDALONE_LINUX"]
flags += [f"UNITY_{year}_{minor}_OR_NEWER" for year in range(2017, 2024) for minor in range(1, 5)]
flags += [f"UNITY_6000_{minor}_OR_NEWER" for minor in range(7)]
compiled_refs = {}
total_sources = 0
for name, definition_path, definition in groups:
    refs = list(framework_refs)
    if not definition.get("noEngineReferences", False):
        refs += engine_refs
    for reference in definition.get("references", []):
        dependency = compiled_refs.get(reference) or package_refs.get(reference)
        if dependency is None:
            parser.error(f"Missing actual dependency DLL for {name}: {reference}")
        refs.append(dependency)
    sources = []
    for source in sorted(definition_path.parent.rglob("*.cs")):
        # A nested asmdef owns its own sources, even when under this folder.
        directory = source.parent
        while directory != definition_path.parent and not list(directory.glob("*.asmdef")):
            directory = directory.parent
        if directory != definition_path.parent:
            continue
        target = out / "source" / source.relative_to(root)
        target.parent.mkdir(parents=True, exist_ok=True)
        encoded = source.read_bytes()
        try:
            text = encoded.decode("utf-8-sig")
        except UnicodeDecodeError:
            text = encoded.decode("gb18030")
        target.write_text(text, encoding="utf-8")
        sources.append(target)
    dll = out / (name + ".dll")
    arguments = [
        "-nologo", "-nostdlib+", "-target:library",
        "-unsafe+" if definition.get("allowUnsafeCode", False) else "-unsafe-", "-langversion:9.0",
        "-define:" + ",".join(flags), "-out:" + str(dll),
    ]
    arguments += ["-r:" + str(path) for path in refs] + [str(path) for path in sources]
    response = out / (name + ".rsp")
    response.write_text("\n".join('"' + argument + '"' for argument in arguments))
    result = subprocess.run([dotnet, csc, "@" + str(response)], capture_output=True, text=True)
    (out / (name + ".log")).write_text(result.stdout + result.stderr)
    errors = [line for line in (result.stdout + result.stderr).splitlines() if "error " in line]
    print(name, len(sources), "sources; exit", result.returncode, "errors", len(errors), flush=True)
    for line in errors[:35]:
        print(line, flush=True)
    if result.returncode:
        sys.exit(result.returncode)
    compiled_refs[name] = dll
    total_sources += len(sources)
print(f"PASS: {len(groups)} assemblies / {total_sources} C# sources ({options.platform}).", flush=True)
