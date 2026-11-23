"""A6 gates: the instrumented run (emulate_adapter_eta.js) must reproduce the earlier rows exactly (sig and old metrics)."""
import glob, json, os, sys
R = os.environ.get('RB_RESULTS') or os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'results')
R = R.replace('\\', '/').rstrip('/') + '/'
KEYS = ['served', 'visRev60', 'late60First', 'early60First', 'sumTV5', 'sumLead', 'nLead', 'breachDeadline60', 'sumS', 'sumWait', 'sumRide']
def load(n): return [json.loads(l) for p in sorted(glob.glob(R + n + '/rows-*.ndjson')) for l in open(p, encoding='utf-8') if l.strip()]
name = sys.argv[1]
new = load(name)
ref = {}
for n in ['fc30', 'padcal-5', 'main60']:
    for r in load(n):
        ref[(r['cell'], r['config'], r['arm'], r['mode'])] = (n, r)
ok = bad = nocmp = 0
for r in new:
    k = (r['cell'], r['config'], r['arm'], r['mode'])
    if k not in ref: nocmp += 1; continue
    n, o = ref[k]
    same = o['sig'] == r['sig'] and all(o[x] == r[x] for x in KEYS)
    if same: ok += 1
    else:
        bad += 1; print('DIFF', k, n, {x: (o[x], r[x]) for x in KEYS if o[x] != r[x]}, o['sig'] == r['sig'])
print(f'{name}: rows {len(new)}, identical to earlier rows {ok}, different {bad}, no earlier counterpart {nocmp}')
print('bad ETA riders:', sum(r['nBadEta'] for r in new), 'dropped late publications:', sum(r['nPubDropped'] for r in new), 'eta riders', sum(r['nEta'] for r in new), 'served', sum(r['served'] for r in new))
sys.exit(1 if bad else 0)
