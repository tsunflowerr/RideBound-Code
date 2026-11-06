"""Rider-by-rider comparison for the final full-system runs (ADR-089 GĐ4/GĐ6): compare_via_full.py itself, with only its
constants replaced in this process (data root, predictions, seal, arm names). The rider rule, the gzip streaming readers
and the G2 move count are compare_via_full.py's, unchanged.

Usage: python -I -B compare_fullsys.py <data root> <predictions.tsv> <seal file> <arm,arm,...> <out-prefix> --root <runs> ...
"""
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import compare_via_full as C  # noqa: E402


def main() -> int:
    data, predictions, seal, arms, prefix, *rest = sys.argv[1:]
    C.DATA = pathlib.Path(data)
    C.OUT_ROOT = C.DATA / "runs"
    C.EMULATOR = pathlib.Path(predictions)
    C.COUNTS = C.EMULATOR.with_name(C.EMULATOR.stem + "-counts.tsv")
    C.SEAL = pathlib.Path(seal)
    C.ARMS = tuple(arms.split(","))
    sys.argv = [sys.argv[0], prefix, *rest]
    return C.main()


if __name__ == "__main__":
    raise SystemExit(main())
