# Phase 30 — First-run, void, and boot order

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/30-first-run-and-boot-order.md`](../../pt_br/plugins/30-first-run-and-boot-order.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md), [28](28-minimal-content-and-empty-core.md), [29](29-worldgen-superflat-plugin.md)

## 1. Goal

Update operator bootstrap (`scripts/first-run.sh`, [`assets/first-run/`](../../../assets/first-run/), [`assets/ci/`](../../../assets/ci/)) and document the **minimum plugin set** for a playable post-extraction server, with default world generator **`void`**.

## 2. Non-goals

- Shipping every first-party plugin in the release zip (recommended ≠ mandatory).
- Forcing `Plugins.Enabled: false` as the production default (keep documented opt-in; first-run may suggest `true` for dogfood).

## 3. Config changes

| File | Field | From | To |
|------|-------|------|-----|
| `assets/first-run/server.json` | `dimensions[].generator` | `superflat` | **`void`** |
| `assets/ci/server.json` | same | (align) | **`void`** |
| Code default in `OrionConfig` if any | generator | `superflat` | **`void`** |
| `docs/*/first-run.md` | prose | mentions superflat | void + how to enable superflat plugin |

Spawn Y in void: adjust docs (keep `[0, -57, 0]` or raise — implementation picks a safe value; first-run doc explains).

## 4. Recommended set (“minimal survival”)

Logical load order (manifest deps do the rest):

1. `orion:minimal-blocks` (+ `orion:minimal-items` if split)
2. `orion:entity-attributes` → `orion:attributes`
3. `orion:entity-gravity` / `collision` / `movement` (and air-supply, equipment as needed)
4. `orion:player-chunk-rendering` (**required** to play)
5. `orion:containers` → `orion:inventory` → `orion:block_containers`
6. `orion:building` / `orion:mining` (opt-in)
7. `orion:creative-fillers` (opt-in)
8. `orion:superflat` **only** if config uses `generator: superflat`

Pure void: items 1–5 (plus entity traits needed for player/item) are enough to join an empty world.

## 5. `build-plugins.sh` / deploy

Update `Plugins-Orion/` script to include new ids in dependency order. Deploy to `OrionServerBE/plugins/<id>/`.

## 6. Example commits

1. `chore(first-run): default world generator to void`
2. `chore(ci-fixtures): align assets/ci server.json generator to void`
3. `docs: document minimal plugin set and superflat opt-in`
4. `chore(build-plugins): add new first-party plugin ids to build order`

No `Co-authored-by`.

## 7. Acceptance tests

- [ ] `first-run.sh` produces `generator: void`.
- [ ] CI fixtures use void (Logger/Game tests still pass — adjust if they assumed superflat layers).
- [ ] First-run doc lists plugins and how to enable superflat.
- [ ] Void boot + minimal-blocks + chunk-rendering allows join.

## 8. Status

`spec`
