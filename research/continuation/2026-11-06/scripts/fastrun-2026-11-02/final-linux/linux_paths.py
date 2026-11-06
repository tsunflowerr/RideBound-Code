"""Linux bundle (2026-11-04): the import folders the comparison and evidence tools need, on the Linux layout.

On Windows the bundle lives at E:/ and the sealed tools add their helper folders with Windows paths (for example
compare_via_full.py and bridge_riders.py insert r"E:\\Code\\...\\tang3\\evidence"). On Linux the same tree is unpacked under
/rb, where those strings are not paths. Instead of editing the sealed tools, the Linux entry points put the right folders
first on sys.path; the tools' own Windows entries are then harmless dead entries.
"""
import sys

ROOT = "/rb"
SCRATCH = ROOT + "/Code/Report_INT3508/ridebound-scratchpad"
KIT = SCRATCH + "/fastrun-2026-11-02"
FOLDERS = [
    KIT + "/fullsys",
    KIT,
    SCRATCH + "/tang3/evidence",
    SCRATCH + "/via-bridge-2026-10-22",
    SCRATCH + "/bridge-phase1-2026-10-23",
]


def install() -> None:
    for folder in reversed(FOLDERS):
        if folder not in sys.path:
            sys.path.insert(0, folder)
