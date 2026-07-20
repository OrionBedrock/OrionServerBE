# Phase 25 — Block mechanics plugins

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/25-block-mechanics-plugins.md`](../../pt_br/plugins/25-block-mechanics-plugins.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md)  
**Code prerequisite:** `BlockTrait` base + detail types + `IBlockTraits` registry in `Orion.Api`

## 1. Goal

Extract orientation traits and reflection registration from [`src/Orion/Block/Traits/`](../../../src/Orion/Block/Traits/) into dedicated plugins. `Block` / `BlockType` / `BlockPermutation` / type components / `BlockRegistry` (no native content) stay in core.

## 2. Non-goals

- Moving the 6 content blocks (phase [28](28-minimal-content-and-empty-core.md)).
- Merging the three direction traits into one plugin **in this spec** (preference: one plugin per trait).
- Moving `BlockDropHelper` (evaluate in 28 with minimal loot).

## 3. Plugins to create

| id | PackageId | Repo | provides | depend | Origin |
|----|-----------|------|----------|--------|--------|
| `orion:block-direction` | `Orion.Plugins.BlockDirection` | `orion-block-direction` | `orion:block-direction` | — | `DirectionTrait.cs` |
| `orion:block-cardinal` | `Orion.Plugins.BlockCardinal` | `orion-block-cardinal` | `orion:block-cardinal` | — | `CardinalDirectionTrait.cs` + enum Types |
| `orion:block-facing` | `Orion.Plugins.BlockFacing` | `orion-block-facing` | `orion:block-facing` | — | `FacingDirectionTrait.cs` + enum Types |

Prefer **Orion.Api** for enums and `Block*Details`. `BlockTrait` + registry API live in Api/host; plugins call `Registries.BlockTraits.Register(...)` in `Load`.

`BlockTypeRotationComponent`: default **core** until component Api is stable (optional later move to cardinal plugin).

## 4. Remove from core

- The three `*Direction*Trait` files (and Types if moved to Api)
- `BlockTraitRegistry.RegisterFromAssembly` for vanilla traits
- Binds that assumed traits live in the Orion assembly

## 5. Relation to building / mining

`orion:building` / `orion:mining` only softdepend orientation traits if place/break need those states.

## 6. Example commits

1. `feat(plugins): add orion:block-direction`
2. `feat(plugins): add orion:block-cardinal`
3. `feat(plugins): add orion:block-facing`
4. `refactor(orion): remove block orientation traits from core`
5. `test: block trait plugins register before catalog freeze`

No `Co-authored-by`.

## 7. Acceptance tests

- [ ] Orientation traits exist only in plugins.
- [ ] Facing/cardinal place behavior matches previous when content uses them.
- [ ] Core does not `RegisterFromAssembly` BlockTraits from Orion.dll.
- [ ] Package/CI match phase [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
