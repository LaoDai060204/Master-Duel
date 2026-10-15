"""Compile a decoded copy of the real Settings source; leave the original intact."""
from pathlib import Path

directory = Path(__file__).resolve().parent
source = directory.parents[2] / "Assets/Scripts/MDPro3/Helper/Settings.cs"
target = directory / "obj/Settings.utf8.cs"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(source.read_bytes().decode("gb18030"), encoding="utf-8")
