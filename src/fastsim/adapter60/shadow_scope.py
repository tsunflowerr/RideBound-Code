"""A6: pham vi chung minh Shadow. Shadow (SHHZ71, SHRC71) so voi cach cap deu 35% (EOHZ35, RCE35) cung co che kiem tra, 60 ngay, G2,
khong du bao (main60). Quy tac gop nhu aggregate.py."""
import collections, glob, json, math, os, sys
from statistics import mean, stdev
sys.stdout.reconfigure(encoding='utf-8')
R = os.path.join(os.environ.get('RB_RESULTS') or os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'results'), 'main60').replace('\\', '/')
rows = [json.loads(l) for p in sorted(glob.glob(R + '/rows-*.ndjson')) for l in open(p, encoding='utf-8') if l.strip()]
T59 = 2.0009957
def per_day(arm):
    g = collections.defaultdict(list)
    for r in rows:
        if r['config'] == 'G2' and r['arm'] == arm and r['mode'] == 'N': g[r['day']].append(r)
    out = {}
    for d, rr in g.items():
        assert len(rr) == 6
        su = lambda k: sum(r[k] for r in rr); sv = su('served')
        out[d] = {'served': sv / 3, 'rev': su('visRev60') / 3, 'late': 1000 * su('late60First') / sv, 'tv5': su('sumTV5') / sv / 1000,
                  'dl': 100 * su('breachDeadline60') / sv, 'early': 1000 * su('early60First') / sv}
    assert len(out) == 60
    return out
def paired(a, b, k):
    v = [a[d][k] - b[d][k] for d in sorted(a)]; m = mean(v); hw = T59 * stdev(v) / math.sqrt(60)
    return f'{m:+.2f} [{m-hw:+.2f}; {m+hw:+.2f}] ({sum(x<0 for x in v)}/{sum(x>0 for x in v)})'
L = {a: per_day(a) for a in ['EO35', 'EOHZ35', 'RCE35', 'SHHZ71', 'SHRC71']}
for a, d in L.items(): print(a, {k: round(mean(x[k] for x in d.values()), 2) for k in ['served', 'rev', 'late', 'early', 'tv5', 'dl']})
for a, b in [('SHHZ71', 'EOHZ35'), ('SHRC71', 'RCE35'), ('SHHZ71', 'EO35')]:
    print(a, '-', b, {k: paired(L[a], L[b], k) for k in ['served', 'rev', 'late', 'tv5']})
