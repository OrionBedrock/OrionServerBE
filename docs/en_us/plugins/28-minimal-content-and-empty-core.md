# Phase 28 — Minimal content and empty core

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/28-minimal-content-and-empty-core.md`](../../pt_br/plugins/28-minimal-content-and-empty-core.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md), [25](25-block-mechanics-plugins.md)  
**Code prerequisite:** stable `RegisterPluginBlock` / item facades; catalog freeze after `Load`

## 1. Goal

1. Remove **all** native registers from [`BlockRegistry.RegisterFromBedrockStates`](../../../src/Orion/Block/BlockRegistry.cs) (6 blocks).
2. Ship minimal content plugin(s) that re-register those blocks (and related items / runtime ids).
3. Enforce load order: **`air` (and the rest) registered before world init**; generators `depend` this content.

## 2. Non-goals

- Full vanilla palette.
- A permanent `air` stub in core (forbidden in the end state — see §4).
- Superflat (phase [29](29-worldgen-superflat-plugin.md)).

## 3. Content to migrate

| Identifier | Notes |
|------------|--------|
| `minecraft:air` | Required for chunks/protocol; highest load priority |
| `minecraft:structure_void` | |
| `minecraft:bedrock` | hardness -1 |
| `minecraft:dirt` | |
| `minecraft:grass_block` | |
| `minecraft:barrier` | non-solid |

Also: related `ItemBlockRuntimeIds` / drops (`BlockDropHelper`) / hardcoded Nature allowlist — clear core and move to plugin.

## 4. Plugins

| id | PackageId | Repo | provides | depend | Role |
|----|-----------|------|----------|--------|------|
| `orion:minimal-blocks` | `Orion.Plugins.MinimalBlocks` | `orion-minimal-blocks` | `orion:minimal-blocks` | — (load early) | Registers the 6 blocks in `Load` |
| `orion:minimal-items` | `Orion.Plugins.MinimalItems` | `orion-minimal-items` | `orion:minimal-items` | **minimal-blocks** | Items / block runtime ids / minimal drops |

**Acceptable alternative:** a single `orion:minimal-blocks` that also registers items. Doc preference: **two plugins** if item footprint grows; otherwise one package for the first delivery.

**creative-fillers:** keeps filling tabs; may `softdepend` minimal-items. Does not replace block registration.

### `air` policy

1. `orion:minimal-blocks` registers `air` in `plugin.Load` (before world bootstrap).
2. Any generator declares `depend` `orion:minimal-blocks` **or** first-run guarantees the plugin on disk and the host fail-fasts if `air` is missing at freeze.
3. Core does **not** `RegisterBlock` content.

## 5. Core changes

- Empty / remove `RegisterFromBedrockStates`.
- Remove hardcoded content hashes from Nature allowlists if any.
- Tests that assumed native dirt/grass load the plugin (or test fixtures register blocks).

## 6. Example commits

1. `feat(plugins): add orion:minimal-blocks with six bedrock hashes`
2. `feat(plugins): add orion:minimal-items runtime id map`
3. `refactor(orion): remove RegisterFromBedrockStates native content`
4. `test: catalog empty without minimal-blocks; air present with plugin`

No `Co-authored-by`.

## 7. Acceptance tests

- [ ] Without plugins: no content blocks (prefer zero + clear failure).
- [ ] With `minimal-blocks`: 6 ids resolve; air present before the first chunk.
- [ ] `orion:superflat` (phase 29) cannot boot without minimal-blocks.
- [ ] NuGet/CI template [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
