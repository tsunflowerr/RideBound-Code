"""Commitment configs (5 arms) and measured-day worlds for the Manhattan bridge.

Arms (company plan step 1.4): U, C-120, C-300 copied byte for byte from tang3/configs;
PS-300 = U + dropEtaRevisionLimitMs 300000 (ADR-080 draft decision 2);
DLHZ-120 = U + dropEtaDeadlineSlackMs 120000 + dropEtaDeadlineRelaxed true (decision 4).
The new keys sit in $.policies[0] after finalConfirmationLocks, where Mplus-120.json keeps
dropEtaDeadlineSlackMs.

Worlds: W5 = the measured day of the cell (sigma 0, no shocks), identical parameters to
tang3/calib/panelA/worlds/W5-d20181114-w08.json; existing files are copied, the four
missing ones (12/13 Nov, w08/w17) are written with only base.day / base.windowStart set.
"""

import hashlib
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TANG3 = r"E:\Code\Report_INT3508\ridebound-scratchpad\tang3"
SRC_CONFIGS = os.path.join(TANG3, "configs")
SRC_WORLDS = os.path.join(TANG3, r"calib\panelA\worlds")
OUT_CONFIGS = os.path.join(HERE, "configs")
OUT_WORLDS = os.path.join(HERE, "worlds")

CELLS = [f"d201811{d}-s10-r1-w08" for d in ("12", "13", "14", "15", "16", "17", "18")] + \
        [f"d201811{d}-s10-r1-w17" for d in ("12", "13", "14", "18")]


def sha(path: str) -> str:
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def write_new(path: str, text: str) -> None:
    if os.path.exists(path):
        if open(path, encoding="utf-8").read() != text:
            raise SystemExit(f"refused: {path} exists with different content")
        return
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def main() -> int:
    os.makedirs(OUT_CONFIGS, exist_ok=True)
    os.makedirs(OUT_WORLDS, exist_ok=True)

    for arm in ("U", "C-120", "C-300"):
        src, dst = os.path.join(SRC_CONFIGS, f"{arm}.json"), os.path.join(OUT_CONFIGS, f"{arm}.json")
        if not os.path.exists(dst):
            shutil.copyfile(src, dst)
        assert sha(src) == sha(dst), arm

    base = json.load(open(os.path.join(SRC_CONFIGS, "U.json"), encoding="utf-8"))
    assert len(base["policies"]) == 1
    assert list(base["policies"][0])[-1] == "finalConfirmationLocks"
    for arm, extra in (("PS-300", {"dropEtaRevisionLimitMs": 300000}),
                       ("DLHZ-120", {"dropEtaDeadlineSlackMs": 120000, "dropEtaDeadlineRelaxed": True})):
        config = json.loads(json.dumps(base))
        config["policies"][0].update(extra)
        write_new(os.path.join(OUT_CONFIGS, f"{arm}.json"), json.dumps(config, indent=2) + "\n")

    template = json.load(open(os.path.join(SRC_WORLDS, "W5-d20181114-w08.json"), encoding="utf-8"))
    for cell in CELLS:
        day = f"2018-11-{cell[7:9]}"
        window = cell[-3:]
        name = f"W5-{cell[:9]}-{window}.json"
        src = os.path.join(SRC_WORLDS, name)
        dst = os.path.join(OUT_WORLDS, name)
        if os.path.exists(src):
            if not os.path.exists(dst):
                shutil.copyfile(src, dst)
            assert sha(src) == sha(dst), name
        else:
            world = json.loads(json.dumps(template))
            world["base"] = {"day": day, "exponent": 1, "windowStart": 28800 if window == "w08" else 61200}
            write_new(dst, json.dumps(world) + "\n")
        check = json.load(open(dst, encoding="utf-8"))
        assert check["base"]["day"] == day, name
        assert check["base"]["windowStart"] == (28800 if window == "w08" else 61200), name
        assert {k: v for k, v in check.items() if k != "base"} == \
               {k: v for k, v in template.items() if k != "base"}, name

    for name in sorted(os.listdir(OUT_CONFIGS)) + sorted(os.listdir(OUT_WORLDS)):
        folder = OUT_CONFIGS if name in os.listdir(OUT_CONFIGS) else OUT_WORLDS
        print(sha(os.path.join(folder, name)), os.path.basename(folder) + "/" + name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
