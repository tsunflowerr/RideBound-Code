# WP7 medium public-derivative physical closure

This driver consumes the byte-locked WP6 medium derivative without changing source
arrival times, request constraints, initial fleet positions or directed travel times.
It materializes the normalized 96-node all-pairs shortest-path snapshot as a directed
FleetPy `NetworkBasic` metric closure and moves actual FleetPy 1.0.2
`SimulationVehicle` instances between exact source arrival epochs. Confirmation is
drained at the same source time before physical movement; long FleetPy movement
updates deliberately exercise multi-stop callback reconstruction.

The audited derivative has exactly 96 nodes. The driver therefore requests one
complete 96-node travel snapshot behind an explicit node bound and the adapter's
bounded 2 MiB Runner frame. Request arrivals do not rebuild the same 9,120
directed arcs; a larger graph fails closed instead of silently degrading scope.

The closure has no source road geometry or source distance field. Its synthetic
distance is excluded from metrics and claims. Results are mechanical Layer-2
compatibility/reproducibility evidence only, never effectiveness, service quality,
fairness, non-inferiority, satisfaction, SLA or novelty evidence.
