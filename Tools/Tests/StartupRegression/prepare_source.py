"""Stage the original GB18030 Boot source for the cross-platform C# compiler."""
from pathlib import Path

project_directory = Path(__file__).resolve().parent
source = project_directory.parents[2] / "Assets/Scripts/MDPro3/Boot.cs"
target = project_directory / "obj/Boot.utf8.cs"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(source.read_bytes().decode("gb18030"), encoding="utf-8")
