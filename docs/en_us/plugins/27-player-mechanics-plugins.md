# Phase 27 — Player mechanics plugins

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/27-player-mechanics-plugins.md`](../../pt_br/plugins/27-player-mechanics-plugins.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md), [24](24-entity-mechanics-plugins.md)  
**Code prerequisite:** `PlayerTrait` / `ISessionTickableTrait` in `Orion.Api`; stable session tick path

## 1. Goal

Extract traits from [`src/Orion/Player/Traits/`](../../../src/Orion/Player/Traits/) into plugins. `Player`, `PlayerSession`, transfer/abilities/NBT merge shells stay in core (Bedrock session).

## 2. Non-goals

- Moving `Player.cs` / `PlayerSession` / world transfer into a plugin.
- Inventory / hunger / building / mining (already plugins).
- Making chunk streaming optional with no replacement — `orion:player-chunk-rendering` is **required** for a playable server with plugins enabled.

## 3. Plugins to create

| id | PackageId | Repo | provides | depend | softdepend | Origin |
|----|-----------|------|----------|--------|------------|--------|
| `orion:player-chunk-rendering` | `Orion.Plugins.PlayerChunkRendering` | `orion-player-chunk-rendering` | `orion:player-chunk-rendering` | — | — | `PlayerChunkRenderingTrait.cs` |
| `orion:player-debug` | `Orion.Plugins.PlayerDebug` | `orion-player-debug` | `orion:player-debug` | — | — | `DebugTrait.cs` |

`ISessionTickableTrait` / `PlayerTrait`: shells in **Orion.Api**.

### Minimal survival boot

Phase [30](30-first-run-and-boot-order.md) lists `orion:player-chunk-rendering` in the recommended set (hard requirement for a playable experience).

## 4. Remove from core

- `PlayerChunkRenderingTrait.cs`, `DebugTrait.cs`
- Automatic registration of those traits on player spawn from the Orion assembly
- Player receives traits via plugin binds / documented defaults

## 5. Example commits

1. `feat(plugins): add orion:player-chunk-rendering`
2. `feat(plugins): add orion:player-debug`
3. `refactor(orion): remove player traits from core`
4. `test: player chunk streaming via plugin trait`

No `Co-authored-by`.

## 6. Acceptance tests

- [ ] Join + chunk streaming works only with the chunk-rendering plugin loaded.
- [ ] Without it, behavior is documented (prefer fail-fast in first-run checklist).
- [ ] Debug trait optional.
- [ ] NuGet/CI template per [22](22-vanilla-extraction-overview.md) §8.

## 7. Status

`spec`
