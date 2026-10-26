"""Refresh pinned USUM expectations from SciresM's two encounter dumps.

Usage: python Tests/EncounterTables/update_fixtures.py ultra-sun.txt ultra-moon.txt
The existing fixture pins each UI area to a source map/table. No application
encounter data or probability code is read when generating expectations.
"""

import argparse
import json
import re
from pathlib import Path


def read_tables(path, names):
    tables = {}
    text = path.read_text(encoding="utf-8-sig")
    for section in re.split(r"^==========\s*$", text, flags=re.M):
        map_match = re.search(r"^Map: (\d+)", section, re.M)
        if not map_match:
            continue
        map_id = int(map_match[1])
        pattern = r"Table (\d+) \((Day|Night)\):\s*Encounters \(Levels (\d+)-(\d+)\): ([^\r\n]+)"
        for row in re.finditer(pattern, section):
            species, rates = [], []
            for label, rate in re.findall(r"(?:^|, )(.+?) \((\d+)%\)", row[5]):
                if label == "(None)" and int(rate) == 0:
                    continue
                form = re.search(r" \(Forme (\d+)\)$", label)
                name = label[:form.start()] if form else label
                species.append(names[name.casefold()] | ((int(form[1]) if form else 0) << 11))
                rates.append(int(rate))
            if sum(rates) != 100:
                raise ValueError(f"Invalid rates at map {map_id}, table {row[1]}")
            key = map_id, int(row[1]), row[2] == "Night"
            if key in tables:
                raise ValueError(f"Duplicate source table {key}")
            tables[key] = dict(LevelMin=int(row[3]), LevelMax=int(row[4]), Species=species, Rates=rates)
    return tables


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ultra_sun", type=Path)
    parser.add_argument("ultra_moon", type=Path)
    args = parser.parse_args()
    directory = Path(__file__).resolve().parent
    root = directory.parents[1]
    names = (root / "3DSRNGTool/Resources/text/text_Species_en.txt").read_text(encoding="utf-8-sig").splitlines()
    names = {name.casefold(): index for index, name in enumerate(names)}
    tables = {7: read_tables(args.ultra_sun, names), 8: read_tables(args.ultra_moon, names)}
    path = directory / "USUM-normal-encounters.json"
    fixtures = json.loads(path.read_text(encoding="utf8"))
    seen = set()
    for fixture in fixtures:
        key = fixture["Version"], fixture["Location"], fixture["Index"], fixture["Night"]
        if key in seen:
            raise ValueError(f"Duplicate fixture {key}")
        seen.add(key)
        source = fixture["SourceMap"], fixture["SourceTable"], fixture["Night"]
        fixture.update(tables[fixture["Version"]][source])
    content = ",\n".join("  " + json.dumps(f, separators=(",", ":")) for f in fixtures)
    path.write_text("[\n" + content + "\n]\n", encoding="utf8")
    print(f"Updated {len(fixtures)} pinned USUM fixtures")


if __name__ == "__main__":
    main()
