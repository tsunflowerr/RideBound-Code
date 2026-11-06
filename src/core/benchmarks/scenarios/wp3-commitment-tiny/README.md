# WP3 commitment tiny demo

This source-controlled correctness transcript exercises the real Runner
`commitment` mode with one vehicle, two requests, and four acknowledged epochs.
It checks an initial promise, an onboard no-change validation, an exogenous-only
revision that consumes no decision-induced budget, a physical capacity rejection,
and a completed rider whose immutable ledger remains in state.

Run from the repository root:

```powershell
./scripts/run-wp3-commitment-demo.ps1
```

The script publishes a self-contained Runner and executes the same input in two
fresh processes. It requires byte-identical output, the four fixed decision
hashes, the fixed final state hash, a produced certificate at every epoch, and
the exact promise publication semantics above. It also checkpoints after the
first committed decision, restores into a fresh process, and requires the
remaining decision suffix to be byte-identical to uninterrupted genesis replay.

`wp3-boundary-test-v1.json` is an explicit named correctness profile. Its
unbounded dimensions and hard-zero identity-switch dimensions are not calibrated
passenger preferences, a production default, or evidence for O-002/O-003. This
tiny bundle makes no effectiveness, scale, simulator, or user-satisfaction claim.
