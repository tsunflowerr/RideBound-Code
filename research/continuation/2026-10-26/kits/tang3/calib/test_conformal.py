"""Tests for conformal.py (KE-HOACH §8.3 required cases; Codex Q01-Q07). Run: python -B test_conformal.py"""
from __future__ import annotations

import conformal as cf

failures: list[str] = []
checks_run = 0
EXPECTED_CHECKS = 30


def check(name: str, condition: bool) -> None:
    global checks_run
    checks_run += 1
    if not condition:
        failures.append(name)
        print("FAIL", name)


def raises(function, *args) -> bool:
    try:
        function(*args)
    except (ValueError, TypeError):
        return True
    return False


# Required rank cases (alpha = 1/20 and 1/10).
check("alpha 0.05, n 18 -> k 19 > n (infinite)", cf.rank(18, 1, 20) == 19 and cf.threshold(list(range(18)), 1, 20).infinite)
check("alpha 0.05, n 19 -> k 19", cf.rank(19, 1, 20) == 19)
check("alpha 0.05, n 20 -> k 20", cf.rank(20, 1, 20) == 20)
check("alpha 0.10, n 8 -> k 9 > n (infinite)", cf.rank(8, 1, 10) == 9 and cf.threshold(list(range(8)), 1, 10).infinite)
check("alpha 0.10, n 9 -> k 9", cf.rank(9, 1, 10) == 9)
check("alpha 0.10, n 20 -> k 19", cf.rank(20, 1, 10) == 19)
# k = n + 1 needs alpha < 1/(n+1): n 20, alpha 1/22 -> k = ceil(21 * 21/22) = 21 (alpha 1/21 gives k = 20).
check("k = n + 1 is infinite, with a reason", cf.rank(20, 1, 21) == 20 and (
    lambda t: t.infinite and t.gamma_ms is None and "k = 21 > n = 20" in t.reason)(cf.threshold(list(range(20)), 1, 22)))
# Integer arithmetic agrees with the definition on a wide range (no float rounding path).
check("rank equals ceil((n+1)(1-a/b)) by exact fractions", all(
    cf.rank(n, a, b) == -((-(n + 1) * (b - a)) // b) for n in range(0, 400) for a, b in ((1, 20), (1, 10), (3, 40), (1, 3))))
check("no overflow on a huge n", cf.rank(10 ** 30, 1, 20) == ((10 ** 30 + 1) * 19 + 19) // 20)
# Order statistic, not interpolation.
scores = [5000, 1000, 3000, 2000, 4000, 6000, 7000, 8000, 9000, 10000]   # n = 10, alpha 1/10 -> k = 10
t = cf.threshold(scores, 1, 10)
check("gamma is the k-th smallest (k 10 of 10 -> max)", t.k == 10 and t.gamma_ms == 10000 and not t.infinite)
t = cf.threshold(scores[:9] + [9500], 1, 5)                                 # alpha 1/5, n 10 -> k = ceil(8.8) = 9
check("alpha 1/5, n 10 -> k 9 and gamma = 9th smallest", t.k == 9 and t.gamma_ms == sorted(scores[:9] + [9500])[8])
check("gamma is always an observed score (no value between order statistics)",
      all(cf.threshold(list(range(0, 1000 * n, 1000)), 1, 10).gamma_ms in range(0, 1000 * n, 1000)
          for n in range(9, 40) if not cf.threshold(list(range(0, 1000 * n, 1000)), 1, 10).infinite))
# Ties are kept.
t = cf.threshold([7, 7, 7, 7, 7, 7, 7, 7, 7, 3], 1, 10)
check("ties: gamma = the tied value", t.gamma_ms == 7 and t.k == 10)
t = cf.threshold([1, 2, 2, 2, 5, 5, 5, 5, 9], 1, 10)
check("ties at the rank are conservative (k 9 of 9 = max)", t.gamma_ms == 9)
# Infinite scores (unobserved episodes) are never turned into numbers.
t = cf.threshold(list(range(9)) + [cf.INFINITE], 1, 10)     # n 10 -> k 10 = the infinite one
check("an infinite score at rank k makes gamma infinite", t.infinite and t.gamma_ms is None)
t = cf.threshold(list(range(19)) + [cf.INFINITE], 1, 10)    # n 20 -> k 19 -> finite 19th smallest
check("an infinite score above rank k leaves gamma finite", not t.infinite and t.gamma_ms == 18)
check("covered: score <= gamma", cf.covered(18, t) and not cf.covered(19, t))
check("covered: an infinite test score is not covered by a finite gamma", not cf.covered(cf.INFINITE, t))
check("covered: an infinite gamma covers everything", cf.covered(cf.INFINITE, cf.threshold([1, 2], 1, 10)))
# Input validation (Q02-Q05).
check("negative score rejected", raises(cf.threshold, [1, -1, 2], 1, 10))
check("float score rejected (integer ms only)", raises(cf.threshold, [1.5, 2], 1, 10))
check("bool score rejected", raises(cf.threshold, [True, 2], 1, 10))
check("alpha outside (0, 1) rejected", raises(cf.rank, 10, 0, 10) and raises(cf.rank, 10, 10, 10))
check("episode in both sets rejected", raises(cf.check_split, {"e1": 1}, {"e1": 2}))
check("episode without a score rejected (empty / incomplete needs a rule)", raises(cf.check_split, {"e1": None}, {"e2": 1}))
check("episode without a declared group rejected", raises(cf.check_split, {"e1": 1}, {"e2": 1}, {"e1": "d14-w07"}))
check("a clean split passes", cf.check_split({"e1": 1}, {"e2": 2}, {"e1": "g", "e2": "g"}) is None)
# Clopper-Pearson.
check("0 of 59 -> upper bound <= 5%", cf.clopper_pearson_upper(0, 59) <= 0.05 < cf.clopper_pearson_upper(0, 58))
check("0 of 29 -> upper bound <= 10%, 0 of 28 above", cf.clopper_pearson_upper(0, 29) <= 0.10 < cf.clopper_pearson_upper(0, 28))
check("closed form for 0 violations matches", abs(cf.clopper_pearson_upper(0, 19) - cf.zero_violation_upper(19)) < 1e-12
      and abs(cf.zero_violation_upper(19) - 0.1458) < 1e-3)

print(f"conformal: {checks_run} checks, {len(failures)} failure(s)")
raise SystemExit(1 if failures or checks_run < EXPECTED_CHECKS else 0)
