"""Stage MyCard.cs using its original GB18030 encoding, without editing it."""
from pathlib import Path
import sys

project_directory = Path(__file__).resolve().parent
source = (project_directory / sys.argv[1]).resolve()
target = project_directory / "obj/MyCard.utf8.cs"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(source.read_bytes().decode("gb18030"), encoding="utf-8")
