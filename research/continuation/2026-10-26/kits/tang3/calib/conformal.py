"""T3.8: split-conformal threshold for the episode score, in exact integer arithmetic (KE-HOACH §8.3; Codex E04, Q01-Q07).

Rule: with n calibration scores and miscoverage alpha = a / b (integers), k = ceil((n + 1) * (1 - alpha)), computed as
k = ((n + 1) * (b - a) + b - 1) // b. If k <= n, gamma = the k-th smallest score (1-based, sorted ascending, ties kept,
so a tie can only make gamma larger or equal: conservative). If k > n, gamma is INFINITE: returned as a flag with a
reason, never encoded as a large number. No library quantile is used (numpy.quantile interpolates and would break the
guarantee).

Inputs are validated (Codex Q02-Q05): scores are non-negative integers (milliseconds) or the INFINITE marker for an
episode whose score was not fully observed (the plan's rule decides when that happens); episode ids are unique; no
episode id is in both the calibration and the test set; groups (e.g. day x window) of the split are declared and
checked. An empty episode (no promised rider) is not silently dropped: it must carry an explicit score (the plan says
which) or it is rejected.
"""
from __future__ import annotations

import math
from dataclasses import dataclass

INFINITE = "infinite"


@dataclass(frozen=True)
class Threshold:
    n: int
    alpha_num: int
    alpha_den: int
    k: int
    gamma_ms: int | None          # None exactly when infinite is True
    infinite: bool
    reason: str


def rank(n: int, alpha_num: int, alpha_den: int) -> int:
    """k = ceil((n + 1)(1 - a/b)) in integers."""
    if not (isinstance(n, int) and isinstance(alpha_num, int) and isinstance(alpha_den, int)):
        raise TypeError("n, alpha_num and alpha_den must be integers")
    if n < 0 or alpha_den <= 0 or not 0 < alpha_num < alpha_den:
        raise ValueError("need n >= 0 and 0 < alpha_num < alpha_den")
    return ((n + 1) * (alpha_den - alpha_num) + alpha_den - 1) // alpha_den


def threshold(scores: list, alpha_num: int, alpha_den: int) -> Threshold:
    for score in scores:
        if score != INFINITE and (not isinstance(score, int) or isinstance(score, bool) or score < 0):
            raise ValueError(f"a score must be a non-negative integer (ms) or INFINITE, got {score!r}")
    n = len(scores)
    k = rank(n, alpha_num, alpha_den)
    if k > n:
        return Threshold(n, alpha_num, alpha_den, k, None, True,
                         f"k = {k} > n = {n}: too few calibration episodes for alpha = {alpha_num}/{alpha_den}")
    finite = sorted(s for s in scores if s != INFINITE)
    if k > len(finite):   # the k-th smallest is an unobserved (infinite) score
        return Threshold(n, alpha_num, alpha_den, k, None, True,
                         f"the {k}-th smallest of {n} scores is an episode with an unobserved score")
    return Threshold(n, alpha_num, alpha_den, k, finite[k - 1], False, f"gamma = the {k}-th smallest of {n} scores")


def check_split(calibration: dict, test: dict, groups: dict | None = None) -> None:
    """calibration / test: episode id -> score. groups: episode id -> group label (every id must have one)."""
    overlap = set(calibration) & set(test)
    if overlap:
        raise ValueError(f"episode in both calibration and test: {sorted(overlap)}")
    for name, part in (("calibration", calibration), ("test", test)):
        if len(set(part)) != len(part):
            raise ValueError(f"duplicate episode id in {name}")
        for episode, score in part.items():
            if score is None:
                raise ValueError(f"{name} episode {episode} has no score (an empty or incomplete episode needs an explicit rule)")
    if groups is not None:
        missing = (set(calibration) | set(test)) - set(groups)
        if missing:
            raise ValueError(f"episodes without a declared group: {sorted(missing)}")


def covered(score, threshold_: Threshold) -> bool:
    """A test episode is covered when its score is <= gamma (an infinite gamma covers everything)."""
    if threshold_.infinite:
        return True
    if score == INFINITE:
        return False
    return score <= threshold_.gamma_ms


def clopper_pearson_upper(violations: int, m: int, confidence: float = 0.95) -> float:
    """One-sided exact upper bound for the violation probability after `violations` in `m` independent episodes."""
    if not 0 <= violations <= m or m <= 0:
        raise ValueError("need 0 <= violations <= m and m > 0")
    if violations == m:
        return 1.0
    from scipy.stats import beta
    return float(beta.ppf(confidence, violations + 1, m - violations))


def zero_violation_upper(m: int, confidence: float = 0.95) -> float:
    """Closed form of the bound above for 0 violations: 1 - (1 - confidence)^(1/m)."""
    return 1.0 - math.pow(1.0 - confidence, 1.0 / m)
