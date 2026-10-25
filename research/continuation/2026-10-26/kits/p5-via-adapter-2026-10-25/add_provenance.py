"""P5 amendment A1: copy the 60 cell provenance files (byte copies, sha-checked) next to the P5 cell sets.
Usage: python -I add_provenance.py   (refuses if cells/provenance exists)
"""
import csv
import hashlib
import os
import shutil

SRC = "E:/RideBoundData/company-intake/2026-10-23/sp/wf14/scenarios/provenance"
ROOT = "E:/RideBoundData/research/p5-via-adapter-v1"


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def main():
    out = ROOT + "/cells/provenance"
    if os.path.exists(out):
        raise SystemExit("refused: " + out + " exists")
    os.makedirs(out)
    rows = list(csv.DictReader(open(ROOT + "/P5-CELLS.tsv", encoding="utf-8"), delimiter="\t"))
    for r in rows:
        name = r["matrix"][:-4] + ".prov.json"
        shutil.copyfile(SRC + "/" + name, out + "/" + name)
        if sha(out + "/" + name) != sha(SRC + "/" + name):
            raise SystemExit("copy mismatch " + name)
    print("provenance files:", len(rows))


if __name__ == "__main__":
    main()
