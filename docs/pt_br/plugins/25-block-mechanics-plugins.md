# Fase 25 — Plugins de mecânica de Block

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/25-block-mechanics-plugins.md`](../../en_us/plugins/25-block-mechanics-plugins.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md)  
**Pré-req código:** `BlockTrait` base + detail types + `IBlockTraits` registry em `Orion.Api`

## 1. Goal

Extrair traits de orientação e o registro reflection de [`src/Orion/Block/Traits/`](../../../src/Orion/Block/Traits/) para plugins dedicados. Shells `Block` / `BlockType` / `BlockPermutation` / components de tipo / `BlockRegistry` (sem conteúdo nativo) ficam no core.

## 2. Non-goals

- Mover os 6 blocos de conteúdo (fase [28](28-minimal-content-and-empty-core.md)).
- Fundir os três traits de direção num único plugin **nesta especificação** (preferência: um plugin por trait; merge opcional só se footprint exigir).
- Mover `BlockDropHelper` (avaliar na 28 junto com loot mínimo).

## 3. Plugins a criar

| id | PackageId | Repo | provides | depend | Origem |
|----|-----------|------|----------|--------|--------|
| `orion:block-direction` | `Orion.Plugins.BlockDirection` | `orion-block-direction` | `orion:block-direction` | — | `DirectionTrait.cs` |
| `orion:block-cardinal` | `Orion.Plugins.BlockCardinal` | `orion-block-cardinal` | `orion:block-cardinal` | — | `CardinalDirectionTrait.cs` + enum Types |
| `orion:block-facing` | `Orion.Plugins.BlockFacing` | `orion-block-facing` | `orion:block-facing` | — | `FacingDirectionTrait.cs` + enum Types |

Enums `CardinalDirection` / `FacingDirection` e `Block*Details`: preferir **Orion.Api**; plugins só implementam traits.

`BlockTrait.cs` + `BlockTraitRegistry` (API de registro): shell no **Orion.Api** / host — plugins chamam `Registries.BlockTraits.Register(...)` em `Load`.

`BlockTypeRotationComponent`: permanece no core (componente de tipo) **ou** move-se para `orion:block-cardinal` se só existir para cardinal — documentar escolha na implementação; default: **core** até Api de components estar estável.

## 4. Remover do core

- Os três `*Direction*Trait.cs` (e Types se movidos ao Api)
- `BlockTraitRegistry.RegisterFromAssembly(Assembly.GetExecutingAssembly())` para traits vanilla
- Qualquer bind que assuma traits no assembly Orion

## 5. Relação com building / mining

`orion:building` / `orion:mining` **softdepend** traits de orientação apenas se place/break dependerem desses states; caso contrário sem dep dura.

## 6. Commits (exemplo)

1. `feat(plugins): add orion:block-direction`
2. `feat(plugins): add orion:block-cardinal`
3. `feat(plugins): add orion:block-facing`
4. `refactor(orion): remove block orientation traits from core`
5. `test: block trait plugins register before catalog freeze`

Sem `Co-authored-by`.

## 7. Acceptance tests

- [ ] Traits de orientação só existem nos plugins.
- [ ] Place com facing/cardinal (quando conteúdo usar) comporta-se como antes.
- [ ] Core não chama `RegisterFromAssembly` para BlockTraits do Orion.dll.
- [ ] Package/CI iguais ao template da fase [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
