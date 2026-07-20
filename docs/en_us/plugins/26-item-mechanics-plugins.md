# Phase 26 — Item mechanics plugins

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/26-item-mechanics-plugins.md`](../../pt_br/plugins/26-item-mechanics-plugins.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md)  
**Code prerequisite:** `ItemTrait` + detail types + item trait registry in `Orion.Api`

## 1. Goal

Extract traits and, when stable, declarative components from [`src/Orion/Item/`](../../../src/Orion/Item/) into plugins. `ItemType` / `ItemStack` / `ItemRegistry` (empty catalog) shells stay in core.

## 2. Non-goals

- Migrating the full curated vanilla item catalog (out of scope).
- Moving food **gameplay** (already in `orion:attributes`) — only declarative food component data.
- The 6 block↔item content mappings (phase [28](28-minimal-content-and-empty-core.md)).

## 3. Plugins to create

| id | PackageId | Repo | provides | depend | Origin |
|----|-----------|------|----------|--------|--------|
| `orion:item-durability` | `Orion.Plugins.ItemDurability` | `orion-item-durability` | `orion:item-durability` | — | `ItemStackDurabilityTrait.cs` |
| `orion:item-debug` | `Orion.Plugins.ItemDebug` | `orion-item-debug` | `orion:item-debug` | — | `ItemDebugTrait.cs` |

### Components

| Component | Destination |
|-----------|-------------|
| `ItemTypeDurabilityComponent` | Prefer Orion.Api (data) + trait in durability plugin |
| `ItemTypeFoodComponent` | Orion.Api (data); behavior in `orion:attributes` |
| Base `ItemTypeComponent` / collection | Orion.Api / core shell |

Detail types: **Orion.Api**.

## 4. Remove from core

- `ItemStackDurabilityTrait`, `ItemDebugTrait`
- `ItemTraitRegistry.RegisterFromAssembly` for Orion.dll traits
- Automatic binds assuming traits in the implementation assembly

Keep: `ItemTrait` base → **Orion.Api**.

## 5. Relation to existing plugins

- `orion:mining` / `orion:building`: softdepend durability if wear-on-use is desired.
- `orion:attributes`: remains owner of food use (`IPlayerItemUseHandler`).

## 6. Example commits

1. `feat(plugins): add orion:item-durability`
2. `feat(plugins): add orion:item-debug`
3. `refactor(orion): remove item traits from core assembly`
4. `test: item trait registration and durability hooks`

No `Co-authored-by`.

## 7. Acceptance tests

- [ ] Durability/debug only via plugins.
- [ ] Item registry freeze still works with external traits.
- [ ] NuGet/CI template per [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
