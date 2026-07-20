# Fase 27 — Plugins de mecânica de Player

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/27-player-mechanics-plugins.md`](../../en_us/plugins/27-player-mechanics-plugins.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md), [24](24-entity-mechanics-plugins.md)  
**Pré-req código:** `PlayerTrait` / `ISessionTickableTrait` em `Orion.Api`; session tick path estável

## 1. Goal

Extrair traits de [`src/Orion/Player/Traits/`](../../../src/Orion/Player/Traits/) para plugins. Shells `Player`, `PlayerSession`, transfer/abilities/NBT merge permanecem no core (sessão Bedrock).

## 2. Non-goals

- Mover `Player.cs` / `PlayerSession` / world transfer para plugin.
- Inventário / hunger / building / mining (já plugins).
- Tornar chunk streaming opcional sem alternativa — `orion:player-chunk-rendering` é **obrigatório** para um servidor jogável com plugins enabled.

## 3. Plugins a criar

| id | PackageId | Repo | provides | depend | softdepend | Origem |
|----|-----------|------|----------|--------|------------|--------|
| `orion:player-chunk-rendering` | `Orion.Plugins.PlayerChunkRendering` | `orion-player-chunk-rendering` | `orion:player-chunk-rendering` | — | — | `PlayerChunkRenderingTrait.cs` |
| `orion:player-debug` | `Orion.Plugins.PlayerDebug` | `orion-player-debug` | `orion:player-debug` | — | — | `DebugTrait.cs` |

`ISessionTickableTrait` / `PlayerTrait`: shells em **Orion.Api**.

### Boot “survival mínimo”

A fase [30](30-first-run-and-boot-order.md) lista `orion:player-chunk-rendering` no conjunto recomendado (hard para experiência jogável).

## 4. Remover do core

- `PlayerChunkRenderingTrait.cs`, `DebugTrait.cs`
- Registro automático desses traits no spawn do player a partir do assembly Orion
- Player passa a receber traits via bind de plugins / defaults documentados

## 5. Commits (exemplo)

1. `feat(plugins): add orion:player-chunk-rendering`
2. `feat(plugins): add orion:player-debug`
3. `refactor(orion): remove player traits from core`
4. `test: player chunk streaming via plugin trait`

Sem `Co-authored-by`.

## 6. Acceptance tests

- [ ] Join + stream de chunks funciona só com o plugin de chunk rendering carregado.
- [ ] Sem o plugin, comportamento documentado (falha clara ou mundo sem stream — preferir fail-fast no first-run checklist).
- [ ] Debug trait opcional.
- [ ] Template NuGet/CI conforme [22](22-vanilla-extraction-overview.md) §8.

## 7. Status

`spec`
