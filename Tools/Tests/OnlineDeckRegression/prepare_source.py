"""Compile the repository's GB18030 source without rewriting its encoding."""
from pathlib import Path
import sys

project = Path(__file__).resolve().parent
source = Path(sys.argv[1])
if not source.is_absolute():
    source = project / source
target = project / "obj/OnlineDeck.utf8.cs"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(source.read_bytes().decode("gb18030"), encoding="utf-8")
