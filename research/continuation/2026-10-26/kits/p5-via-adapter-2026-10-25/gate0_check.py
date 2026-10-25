"""P5 gate 0 only (same rule as compare_p5.py): home plain rows vs company confirm rows, all fields except IGNORE.
Usage: python -I gate0_check.py <company confirm.ndjson> <home plain rows>
"""
import json
import sys

IGNORE = {"part", "set", "ms", "msScore"}


def load(path):
    out = {}
    for line in open(path, encoding="utf-8"):
        if line.strip():
            r = json.loads(line)
            out[(r["cell"], r["config"], r["arm"])] = {k: v for k, v in r.items() if k not in IGNORE}
    return out


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    company, plain = load(sys.argv[1]), load(sys.argv[2])
    same = [k for k in plain if company.get(k) == plain[k]]
    diff = [k for k in plain if k in company and company[k] != plain[k]]
    missing = [k for k in plain if k not in company]
    print("home plain rows %d; identical to company %d; different %d; not in company %d" % (len(plain), len(same), len(diff), len(missing)))
    for k in diff[:10]:
        fields = sorted(f for f in set(plain[k]) | set(company[k]) if plain[k].get(f) != company[k].get(f))
        print("  DIFF", k, [(f, plain[k].get(f), company[k].get(f)) for f in fields[:5]])
    print("GATE 0", "PASS" if len(same) == len(plain) == 840 else "NOT YET / FAIL")


if __name__ == "__main__":
    main()
