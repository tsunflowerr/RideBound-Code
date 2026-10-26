# verify-fullsys DEVIATIONS (thăm dò sau kiểm toán)
- V-D1 (2026-10-26T04:48:20Z): first run of scripts/indep.js wrote results/indep.txt with its section headers missing
  (say() took one argument; headers were passed as a second argument). Numbers, gates, comparison and labels were unaffected.
  Fixed say/gsay to take several arguments and added an output suffix argument; re-run writes results/*_v2.* ; the first outputs are kept.
