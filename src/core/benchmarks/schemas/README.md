# RideBound protocol schemas

Machine-readable protocol assets live under `v1/`. All `$id` values are stable
repository URLs and all `$ref` values are relative, so the same files work from
the repository or from a copied artifact bundle.

- `v1/schema-inventory.json` maps contract types and messages to schema files.
- `v1/compatibility-matrix.json` records executable version behavior.
- `v1/event-batch.schema.json`, `decision.schema.json`,
  `decision-applied.schema.json` and `error.schema.json` lock the remaining Q1
  message shapes.
- `fixtures/` contains valid and invalid examples used by .NET contract tests
  and future cross-language adapters.

Schema v1 is strict: omitted optional fields are allowed only where a schema says
so, `null` is not used, and unknown fields are rejected. A future minor field is
safe only after an explicit compatibility profile names the field and its
ignore/default behavior.

Post-outcome research schemas that are not Runner protocol contracts live in their
own namespaces. `wp13/v1/first-divergence-record-set.schema.json` is the strict,
self-contained schema for exploratory H6 paired first-divergence records; it does not
change protocol schema v1 or the frozen H6 artifacts.
`wp13/v1/recorded-witness-relaxation-set.schema.json` is the strict
recorded-witness-only clearance contract; retained portfolios and post-clearance
candidate feasibility remain explicitly unevaluated.
`wp13/v1/mechanism-classification-set.schema.json` joins those links to immediate
behavior using noncausal evidence classes and an explicit ranking/search-omission
indeterminate state.
`wp13/v1/option-set-sufficiency-set.schema.json` records count-only H6 option-set
covariates, explicit missing fields and the evidence-vNext decision boundary.
`wp13/v1/runner-retained-candidate-portfolio-evidence.schema.json` is the strict
opt-in Runner v1.2 portfolio payload for generated/eligible/selected candidates and
exact solver-neutral objective inputs; it does not alter historical H6 evidence.
`wp13/v1/exploratory-replay-freeze.schema.json` binds the post-outcome E1 target,
runtime, output/resource envelope and source hash DAG before execution.
`wp13/v1/exploratory-replay-inventory.schema.json` records exact 80-arm bundle and
v1.2 coverage receipts after execution without making a mechanism conclusion.
`wp13/v1/e1-falsification-receipt.schema.json` locks exact 31 in-memory mutations,
typed rejection codes and 80-arm/44,156-portfolio independent reconstruction.
`wp13/v1/e1-h6-behavioral-equivalence.schema.json` records exact same-arm E1-to-H6
operational projection equality without requiring instrumentation-bound state hashes.
`wp13/v1/e1-candidate-descriptive-aggregation.schema.json` locks the exact 40-pair
semantic-signature join, objective-profile incomparability and overlapping/non-additive
finite-panel association boundary.
`wp13/v1/full-source-logic-claim-audit.schema.json` binds the pre-closure WP13
repository inventory, historical/current verifier identities, complete external
artifact DAG, deep H6/E1 raw rescan and zero-unresolved-P0-to-P2 gate without adding
a scientific result.
`wp13/v1/closure-decision.schema.json` binds the final WP13 exit gates, all three P3
resolutions, raw-retention/successor-verifier policy and the constrained decision on
whether WP14 exploratory ablation may open without rescuing H6.
