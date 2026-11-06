# WP2 tiny online demo

This source-controlled transcript exercises the WP2 B1 online path with one
vehicle, three nodes, two requests, and four acknowledged epochs:

1. bootstrap travel times and vehicle state; accept `r-1` and publish its route;
2. confirm, reach pickup, and board `r-1`;
3. reject `r-2` with the physical `CAPACITY` witness because party size 5 exceeds
   vehicle capacity 4;
4. reach drop-off and complete `r-1`.

From the repository root on PowerShell:

```powershell
./scripts/run-wp2-tiny-demo.ps1
```

The command publishes a clean self-contained runner, replays the input twice,
and requires byte-exact output plus the published final decision hash. Output
and hash files are fixed review artifacts; the script never regenerates them.

Manifest content hashes are explicit synthetic fixture identities, and the
adapter/simulator labels identify the NDJSON fixture driver—not FleetPy or a
production deployment. They bind this regression transcript but are not a
release-binary attestation.

This demo is correctness evidence only for the tiny published bound. WP2 has no
commitment ledger, hard revision budget, valid commitment certificate,
performance claim, or simulator integration.
