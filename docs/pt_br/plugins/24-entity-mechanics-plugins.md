# Fase 24 — Plugins de mecânica de Entity

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/24-entity-mechanics-plugins.md`](../../en_us/plugins/24-entity-mechanics-plugins.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md)  
**Pré-req código:** trait registries + `IEntity` / detail types em `Orion.Api` ([12](12-sdk-registries-traits.md))

## 1. Goal

Extrair traits e tipos auxiliares de [`src/Orion/Entity/Traits/`](../../../src/Orion/Entity/Traits/) (e `ItemEntity`) para **plugins first-party dedicados**, um id por mecânica genérica, com deps corretas. Shell `Entity` / `EntityType` / metadata / `EntityRegistry` (vazio ou mínimo) permanece no core via Api.

## 2. Non-goals

- Mover `Entity.cs`, `EntityRegistry` inteiro, ou metadata de protocolo para plugin.
- Criar `orion:entity-damage` separado — dano/heal permanece em **`orion:attributes`**.
- Reimplementar health/hunger (já em attributes).

## 3. Plugins a criar

| id | PackageId | Repo | provides | depend | softdepend | Origem core |
|----|-----------|------|----------|--------|------------|-------------|
| `orion:entity-gravity` | `Orion.Plugins.EntityGravity` | `orion-entity-gravity` | `orion:entity-gravity` | — | — | `EntityGravityTrait.cs` |
| `orion:entity-collision` | `Orion.Plugins.EntityCollision` | `orion-entity-collision` | `orion:entity-collision` | — | — | `EntityCollisionTrait.cs` |
| `orion:entity-movement` | `Orion.Plugins.EntityMovement` | `orion-entity-movement` | `orion:entity-movement` | — | gravity, collision | `EntityMovementTrait.cs` + Types move/teleport/rendered/fall |
| `orion:entity-attributes` | `Orion.Plugins.EntityAttributes` | `orion-entity-attributes` | `orion:entity-attribute-runtime` | — | — | `EntityAttributeTrait.cs`, `AttributeProperties.cs` |
| `orion:entity-air-supply` | `Orion.Plugins.EntityAirSupply` | `orion-entity-air-supply` | `orion:entity-air-supply` | **`orion:attributes`** `[1.0,99.0]` | — | `EntityAirSupplyTrait.cs` (usa `IEntityHealthService`) |
| `orion:entity-equipment` | `Orion.Plugins.EntityEquipment` | `orion-entity-equipment` | `orion:entity-equipment` | — | `orion:containers` | `EntityEquipmentTrait.cs` |
| `orion:item-entity` | `Orion.Plugins.ItemEntity` | `orion-item-entity` | `orion:item-entity` | — | movement, gravity, collision | `ItemEntity.cs` + registro tipo `minecraft:item` se sair do core |

### Ajuste em plugin existente

| id | Mudança |
|----|---------|
| `orion:attributes` | Adicionar `depend` (ou softdepend hard-ordered) em `orion:entity-attributes` para a base `EntityAttributeTrait` |

### Tipos / enums

- `EntityInteractMethod`, `EntitySpawnOptions`, `EntityDeathOptions`, `EntityDespawnOptions`, etc.: preferir **Orion.Api** (contratos), não duplicar em cada plugin. Plugins só **registram** traits concretos.
- Sinais `EntitySpawnSignal` / `EntityHurtSignal` / `EntityDieSignal`: catálogo Api ([13](13-sdk-events-signals.md)); emissão continua no dono da mecânica (attributes para hurt/die).

## 4. EntityRegistry nativos (`player` / `item`)

**Decisão travada nesta fase:**

- Tipo **`player`**: stub mínimo permanece no core (sessão exige player type) até fase player/content decidir o contrário.
- Tipo **`item`** + classe `ItemEntity`: migram para `orion:item-entity` (registro em `Load` via facade EntityTypes quando Api existir; senão documentar gap em 23).

## 5. Remover do core (após plugins verdes)

- `Entity/Traits/EntityGravityTrait.cs`
- `EntityCollisionTrait.cs`, `EntityMovementTrait.cs`, `EntityAttributeTrait.cs`, `EntityAirSupplyTrait.cs`, `EntityEquipmentTrait.cs`
- `EntityTraitRegistry.RegisterFromAssembly` para esses tipos
- `ItemEntity.cs` (quando plugin cobrir)
- Bind automático no `EntityType` que assumia traits no assembly Orion

Manter: `EntityTrait.cs` base → mover para **Orion.Api** (não plugin).

## 6. Ordem de commits (exemplo)

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

Sem `Co-authored-by`. Push `development` → PR → `main` (publish auto se sources mudarem).

## 7. Acceptance tests

- [ ] Boot com plugins de entity traits; sem traits duplicados no Orion.dll.
- [ ] Item drops / movement / drowning (air-supply + attributes) funcionam.
- [ ] `orion:attributes` falha o boot se `entity-attributes` ausente (se hard depend).
- [ ] Nenhum `ProjectReference` Orion no estado final.
- [ ] Game.Tests ajustados / verdes.

## 8. Status

`spec`
