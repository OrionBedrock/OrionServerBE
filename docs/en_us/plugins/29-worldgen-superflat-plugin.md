# Phase 29 — Worldgen: Superflat plugin and void in core

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/29-worldgen-superflat-plugin.md`](../../pt_br/plugins/29-worldgen-superflat-plugin.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md), [28](28-minimal-content-and-empty-core.md)

## 1. Goal

1. Remove `SuperFlatGenerator` from [`GeneratorFactory`](../../../src/World/Generation/GeneratorFactory.cs) builtins.
2. Ship `orion:superflat` registering generator id `superflat` via `IGeneratorRegistry` in `Load`/`OnEnable`.
3. Core keeps only the **`void`** builtin (unknown → void fallback).
4. Confirm generation stays **synchronous** (`Generate`); no async API requirement.

## 2. Non-goals

- Advanced terrain / biomes.
- Mandatory parallel pregen (world AreaShards already multithread; bootstrap pregen may stay sync).
- First-run template edits (phase [30](30-first-run-and-boot-order.md) sets `generator: void`).

## 3. Plugin

| Field | Value |
|-------|--------|
| id | `orion:superflat` |
| PackageId | `Orion.Plugins.Superflat` |
| Repo | `OrionBedrock/orion-superflat` |
| provides | `orion:superflat` |
| depend | **`orion:minimal-blocks`** `[1.0,99.0]` |
| Origin | [`SuperFlatGenerator.cs`](../../../src/World/Generation/SuperFlatGenerator.cs) |

Current layers (baseY −64): bedrock → 3× dirt → grass — require minimal-blocks ids.

Implementation: generator class in the plugin (same `Generator` contract via Api / shared type), `Registries.Generators.Register("superflat", ...)`.

## 4. Core changes

```csharp
// GeneratorFactory builtins (target)
"void" => new VoidGenerator(),
_ => new VoidGenerator()
// no "superflat" case
```

- Move `SuperFlatGenerator.cs` into the plugin (or reimplement against Api).
- Config defaults: phase 30; code default string → `"void"`.

## 5. Threading / async (clarification)

| Layer | Today | This phase |
|-------|--------|------------|
| `Generator.Generate` | Sync | Sync |
| `ChunkPregenerator` | Sync bootstrap loop | Sync OK |
| Area workers | Multithreaded world | Outside the generator |

Future parallel pregen via plugin = optional Api extension (phase 23) — **does not block** 29.

## 6. Example commits

1. `feat(plugins): add orion:superflat generator plugin`
2. `refactor(world): remove superflat builtin from GeneratorFactory`
3. `chore(config): default generator string to void` (may share PR with 30)
4. `test: superflat loads only with plugin; void without`

No `Co-authored-by`.

## 7. Acceptance tests

- [ ] Without plugin: `generator: superflat` fails or falls back to void **as documented**; prefer explicit error for unknown ids after freeze.
- [ ] With plugin + minimal-blocks: superflat layers match current behavior.
- [ ] `depend` minimal-blocks blocks boot if blocks missing.
- [ ] NuGet/CI template [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
