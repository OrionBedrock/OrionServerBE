# Phase 24 — Entity mechanics plugins

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/24-entity-mechanics-plugins.md`](../../pt_br/plugins/24-entity-mechanics-plugins.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md)  
**Code prerequisite:** trait registries + `IEntity` / detail types in `Orion.Api` ([12](12-sdk-registries-traits.md))

## 1. Goal

Extract traits and helpers from [`src/Orion/Entity/Traits/`](../../../src/Orion/Entity/Traits/) (and `ItemEntity`) into **dedicated first-party plugins**, one id per generic mechanic, with correct deps. `Entity` / `EntityType` / metadata / `EntityRegistry` shells stay in core via Api.

## 2. Non-goals

- Moving `Entity.cs`, the whole `EntityRegistry`, or protocol metadata into a plugin.
- Creating a separate `orion:entity-damage` — damage/heal stays in **`orion:attributes`**.
- Reimplementing health/hunger (already in attributes).

## 3. Plugins to create

| id | PackageId | Repo | provides | depend | softdepend | Core origin |
|----|-----------|------|----------|--------|------------|-------------|
| `orion:entity-gravity` | `Orion.Plugins.EntityGravity` | `orion-entity-gravity` | `orion:entity-gravity` | — | — | `EntityGravityTrait.cs` |
| `orion:entity-collision` | `Orion.Plugins.EntityCollision` | `orion-entity-collision` | `orion:entity-collision` | — | — | `EntityCollisionTrait.cs` |
| `orion:entity-movement` | `Orion.Plugins.EntityMovement` | `orion-entity-movement` | `orion:entity-movement` | — | gravity, collision | `EntityMovementTrait.cs` + move/teleport types |
| `orion:entity-attributes` | `Orion.Plugins.EntityAttributes` | `orion-entity-attributes` | `orion:entity-attribute-runtime` | — | — | `EntityAttributeTrait.cs`, `AttributeProperties.cs` |
| `orion:entity-air-supply` | `Orion.Plugins.EntityAirSupply` | `orion-entity-air-supply` | `orion:entity-air-supply` | **`orion:attributes`** `[1.0,99.0]` | — | `EntityAirSupplyTrait.cs` |
| `orion:entity-equipment` | `Orion.Plugins.EntityEquipment` | `orion-entity-equipment` | `orion:entity-equipment` | — | `orion:containers` | `EntityEquipmentTrait.cs` |
| `orion:item-entity` | `Orion.Plugins.ItemEntity` | `orion-item-entity` | `orion:item-entity` | — | movement, gravity, collision | `ItemEntity.cs` + `minecraft:item` type if leaving core |

### Existing plugin change

| id | Change |
|----|--------|
| `orion:attributes` | Add `depend` (or hard-ordered softdepend) on `orion:entity-attributes` for base `EntityAttributeTrait` |

### Types / enums

- Prefer **Orion.Api** for shared option/detail types — plugins only **register** concrete traits.
- Hurt/die/spawn signals: Api catalog; emission stays with the mechanic owner (attributes for hurt/die).

## 4. Native EntityRegistry types (`player` / `item`)

**Locked for this phase:**

- **`player` type:** minimal stub remains in core (session requires it) until a later decision.
- **`item` type** + `ItemEntity` class: migrate to `orion:item-entity`.

## 5. Remove from core (after plugins are green)

- Gravity / collision / movement / attribute / air-supply / equipment trait files
- `EntityTraitRegistry.RegisterFromAssembly` for those types
- `ItemEntity.cs` (when plugin covers it)
- Automatic `EntityType` binds that assumed traits live in the Orion assembly

Keep: `EntityTrait` base → move to **Orion.Api** (not a plugin).

## 6. Example commit order

1. `feat(plugins): add orion:entity-gravity scaffold and NuGet metadata`
2. `feat(plugins): add orion:entity-collision`
3. `feat(plugins): add orion:entity-movement with softdeps`
4. `feat(plugins): add orion:entity-attributes runtime trait`
5. `feat(plugins): wire orion:attributes depend on entity-attributes`
6. `feat(plugins): add orion:entity-air-supply`
7. `feat(plugins): add orion:entity-equipment`
8. `feat(plugins): add orion:item-entity`
9. `refactor(orion): remove migrated entity traits from core`
10. `test: cover entity trait plugins load order`

No `Co-authored-by`.

## 7. Acceptance tests

- [ ] Boot with entity trait plugins; no duplicate traits in Orion.dll.
- [ ] Item drops / movement / drowning (air-supply + attributes) work.
- [ ] `orion:attributes` fails boot if `entity-attributes` missing (if hard depend).
- [ ] No `ProjectReference` to Orion in the final state.
- [ ] Game.Tests updated / green.

## 8. Status

`spec`
