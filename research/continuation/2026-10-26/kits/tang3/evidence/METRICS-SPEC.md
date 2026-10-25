# Tier 3 episode metrics — specification (v1.1, 2026-09-25)

v1.1 resolves the nine ambiguities the independent verifier reported on v1. None of them changed a
number on the two smoke episodes. They are fixed now, before any pilot data exists:

1. **CVaR tail.** Start at index `(95 · n) // 100`, in integer arithmetic.
2. **Rider never boarded.** A rider without `actualPickupTimeMs` has a pickup coordinate that never
   closes.
3. **Breach without `kind`.** It is labelled `operationalIncident`.
4. **Acknowledgements.** They are counted as distinct decision hashes. An acknowledgement only counts
   when it comes after its decision.
5. **Missing certificate.** A missing certificate or body is not non-normal; only
   `normalOperation == false` counts.
6. **Chain continuity.** It is checked on the ETAs of the coordinate.
7. **"Exactly once" in cross-check 2.** It is counted over acknowledged decisions, and `requestId`
   must match too.
8. **E2.** It uses strict `>` 60 000 ms, and the denominator is promised riders, not all requests.
9. **Pickup sums.** They are reported in addition to the table, as `sumPickupC`, `sumPickupX` and
   `sumPickupV`. Open rows only.

This is the definition an independent verifier implements from scratch. It must read only the raw
transcript and this file, not `audit_vep.py`.

## Input

One `transcript-00.ndjson` per episode. Each line is a JSON object with `direction`
(`adapterToRunner` or `runnerToAdapter`), `frameBase64`, and `frameSha256`. `frameSha256` is the
SHA-256 of the base64-decoded bytes. Each decoded frame is a protocol envelope (JSON) with
`messageType`, optional `simTimeMs`, and `payload`.

- A **decision** is a `runnerToAdapter` frame with `messageType == "decision"`. Its
  `payload.decisionHash` identifies it.
- A decision is **acknowledged** when an `adapterToRunner` frame with
  `messageType == "decisionApplied"` has the same `payload.decisionHash`.
- The **final state** is `payload.content.onlineState` of the last `runnerToAdapter` frame with
  `messageType == "checkpoint"`. An episode without it has no final state and is reported with
  `finalCheckpoint: false`.

## Per-rider promise history (from the final state)

- `onlineState.commitmentLedger` is a list of histories `{requestId, entries}`.
- Entry 0 has `kind == "initialPromise"`. Every later entry has `kind == "revision"`.
- Every entry has `publishedPromise.projection.{pickupEtaMs, dropEtaMs}` (that is `p`) and
  `publishedPromise.publishedAtMs`.
- A revision also has `previousPromise.projection.*` (`v`) and `exogenousProjection.*` (`e`).
- For the initial entry, `v = e = p`.

For each rider and each coordinate κ ∈ {pickup, drop}, and each revision t:

    x_t = e_t − v_t        c_t = p_t − e_t        z_t = p_t − v_t

These use exact integers in milliseconds.

The **pickup coordinate closes** at the rider's actual boarding: `requests[*].actualPickupTimeMs`.
A pickup revision with `publishedAtMs ≥ actualPickupTimeMs` does not accumulate. The drop coordinate
never closes within a history.

Accumulate over the open revisions of each rider:

    C = Σ|c_t|     X = Σ|x_t|     V = Σ|z_t|

Also check that the chain is continuous: every revision's `v` equals the previous entry's `p`, also
through closed rows.

## Episode table

Report these fields:

| Field | Definition |
|---|---|
| `requests` | number of `onlineState.requests` |
| `lifecycle` | count of requests per `lifecycle` value |
| `E1ServedShare` | `lifecycle["completed"] / requests` |
| `promisedRiders` | number of commitment histories |
| `E2ShareDropVAbove60s` | share of promised riders whose **drop** `V` > 60 000 ms |
| `dropVCvar95Ms` | mean of the largest ⌈5 %⌉ drop `V` values, as below |
| `sumDropC`, `sumDropX`, `sumDropV` | sums over promised riders of drop `C`, `X`, `V` |
| `decisionsAcknowledged` | number of acknowledged decisions |
| `nonNormalCertificates` | acknowledged decisions with `payload.certificate.body.normalOperation == false` |
| `breaches` | count of `onlineState.incidentLedger.breaches` by `kind`; a missing `kind` is an operational-incident breach |
| `latePickups` | number of `onlineState.incidentLedger.latePickups` (0 if the key is absent) |
| `latePickupMaxMs` | max of `actualPickupMs − latestPickupMs` over late pickups (0 if none) |

For `dropVCvar95Ms`: sort the drop `V` values ascending, then take the mean of the values from index
`floor(0.95 · n)` to the end.

## Cross-checks the verifier must also run

1. For every revision, `|x|`, `|c|` and `|z|` of the drop coordinate equal the entry's
   `deltas.exogenous.dropEtaTotalMs`, `deltas.decisionInduced.dropEtaTotalMs` and
   `deltas.visible.dropEtaTotalMs`. The pickup coordinate uses `pickupEtaTotalMs` the same way.
2. Every ledger entry's `publicationId` appears exactly once as a `promisePublished` action
   (`payload.actions[*]` with `decisionType == "promisePublished"`, `payload.publicationId`) of an
   acknowledged decision, with equal `promise.pickupEtaMs` / `promise.dropEtaMs`. No acknowledged
   publication is missing from the ledger.
