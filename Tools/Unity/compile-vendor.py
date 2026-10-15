#!/usr/bin/env python3
"""Compile vendored assemblies against installed Unity DLLs, without running Unity.

This is a C# API compatibility check, not a Unity import or player build.
Sources are staged in a temporary directory; no Unity DLLs are redistributed.
"""
from pathlib import Path
import argparse
import subprocess
import sys
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--editor-root", type=Path, default=Path("/workspace/.tools/unity/6000.6.3f1/Editor"))
parser.add_argument("--dotnet", default="/workspace/.tools/dotnet/dotnet")
parser.add_argument("--output", type=Path)
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
refs = [data / "NetStandard/ref/2.1.0/netstandard.dll"]
refs += list((data / "Managed/UnityEngine").glob("*.dll"))
refs += [f for f in cache.glob("*.dll") if not f.name.startswith("Assembly-CSharp")]
print("Compiler:", csc, flush=True)
print("Logs and staged compilation outputs:", out, flush=True)
groups = [
    ("Unity.AssetBundleBrowser.Editor", "Assets/Tools/AssetBundles-Browser-1.7.0/Editor"),
    ("spine-csharp", "Assets/Tools/Spine/Runtime/spine-csharp"),
    ("spine-unity", "Assets/Tools/Spine/Runtime/spine-unity"),
    ("spine-unity-editor", "Assets/Tools/Spine/Editor"),
]
flags = [
    "UNITY_EDITOR", "UNITY_EDITOR_LINUX", "UNITY_STANDALONE", "UNITY_STANDALONE_LINUX",
    "UNITY_5_3_OR_NEWER", "UNITY_5_4_OR_NEWER", "UNITY_5_5_OR_NEWER", "UNITY_5_6_OR_NEWER",
]
flags += [f"UNITY_{year}_{minor}_OR_NEWER" for year in range(2017, 2024) for minor in range(1, 5)]
flags += [f"UNITY_6000_{minor}_OR_NEWER" for minor in range(7)]
for name, folder in groups:
    sources = []
    for source in (root / folder).rglob("*.cs"):
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
        "-nologo", "-nostdlib+", "-target:library", "-unsafe-", "-langversion:9.0",
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
    refs.append(dll)
