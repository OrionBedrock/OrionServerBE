# Fase 26 — Plugins de mecânica de Item

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/26-item-mechanics-plugins.md`](../../en_us/plugins/26-item-mechanics-plugins.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md)  
**Pré-req código:** `ItemTrait` + detail types + item trait registry em `Orion.Api`

## 1. Goal

Extrair traits e, quando estável, components declarativos de [`src/Orion/Item/`](../../../src/Orion/Item/) para plugins. Shells `ItemType` / `ItemStack` / `ItemRegistry` (catálogo vazio) ficam no core.

## 2. Non-goals

- Migrar o catálogo curated completo de itens vanilla (fora de escopo).
- Mover food **gameplay** (já em `orion:attributes` / food trait do plugin attributes) — só o component declarativo no core Item/.
- Conteúdo dos 6 blocos↔itens (fase [28](28-minimal-content-and-empty-core.md), inclui `ItemBlockRuntimeIds` limpeza).

## 3. Plugins a criar

| id | PackageId | Repo | provides | depend | Origem |
|----|-----------|------|----------|--------|--------|
| `orion:item-durability` | `Orion.Plugins.ItemDurability` | `orion-item-durability` | `orion:item-durability` | — | `ItemStackDurabilityTrait.cs` + uso de `ItemTypeDurabilityComponent` |
| `orion:item-debug` | `Orion.Plugins.ItemDebug` | `orion-item-debug` | `orion:item-debug` | — | `ItemDebugTrait.cs` |

### Components

| Component | Destino |
|-----------|---------|
| `ItemTypeDurabilityComponent` | Preferir Orion.Api (dado) + trait no plugin durability |
| `ItemTypeFoodComponent` | Orion.Api (dado); comportamento em `orion:attributes` |
| `ItemTypeComponent` base / collection | Orion.Api / core shell |

Detail types (`ItemPlaceDetails`, `ItemUseOn*`, …): **Orion.Api**.

## 4. Remover do core

- `ItemStackDurabilityTrait`, `ItemDebugTrait`
- `ItemTraitRegistry.RegisterFromAssembly` para traits do Orion.dll
- Binds automáticos que assumem traits no assembly de implementação

Manter: `ItemTrait` base → **Orion.Api**.

## 5. Relação com plugins existentes

- `orion:mining` / `orion:building`: softdepend durability se wear-on-use for desejado.
- `orion:attributes`: continua dono de food use (`IPlayerItemUseHandler`).

## 6. Commits (exemplo)

1. `feat(plugins): add orion:item-durability`
2. `feat(plugins): add orion:item-debug`
3. `refactor(orion): remove item traits from core assembly`
4. `test: item trait registration and durability hooks`

Sem `Co-authored-by`.

## 7. Acceptance tests

- [ ] Durability/debug só via plugins.
- [ ] Item registry freeze ainda funciona com traits externos.
- [ ] Template NuGet/CI conforme [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
