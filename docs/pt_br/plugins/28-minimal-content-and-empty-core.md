# Fase 28 — Conteúdo mínimo e core vazio

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/28-minimal-content-and-empty-core.md`](../../en_us/plugins/28-minimal-content-and-empty-core.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md), [25](25-block-mechanics-plugins.md)  
**Pré-req código:** `RegisterPluginBlock` / item facades estáveis; freeze de catálogo após `Load`

## 1. Goal

1. Remover **todos** os registers nativos de [`BlockRegistry.RegisterFromBedrockStates`](../../../src/Orion/Block/BlockRegistry.cs) (6 blocos).
2. Publicar plugin(s) de conteúdo mínimo que re-registrem esses blocos (e itens / runtime ids associados).
3. Garantir load order: **`air` (e demais) registrados antes do world init**; generators `depend` este conteúdo.

## 2. Non-goals

- Paleta vanilla completa.
- Manter stub permanente de `air` no core (proibido no estado final — ver §4).
- Superflat (fase [29](29-worldgen-superflat-plugin.md)).

## 3. Conteúdo a migrar

| Identifier | Notas |
|------------|--------|
| `minecraft:air` | Obrigatório para chunks/protocolo; prioridade máxima de load |
| `minecraft:structure_void` | |
| `minecraft:bedrock` | hardness -1 |
| `minecraft:dirt` | |
| `minecraft:grass_block` | |
| `minecraft:barrier` | não-sólido |

Também: entradas correlatas em `ItemBlockRuntimeIds` / drops (`BlockDropHelper`) / creative Nature se ainda houver allowlist hardcoded — limpar core e mover para plugin.

## 4. Plugins

| id | PackageId | Repo | provides | depend | Papel |
|----|-----------|------|----------|--------|-------|
| `orion:minimal-blocks` | `Orion.Plugins.MinimalBlocks` | `orion-minimal-blocks` | `orion:minimal-blocks` | — (load cedo) | Registra os 6 blocos em `Load` |
| `orion:minimal-items` | `Orion.Plugins.MinimalItems` | `orion-minimal-items` | `orion:minimal-items` | **minimal-blocks** | Itens/block runtime ids / drops mínimos |

**Alternativa aceitável:** um único `orion:minimal-blocks` que também registra itens (menos repos). Preferência doc: **dois plugins** se o footprint de itens crescer; senão um mono-plugin na primeira entrega.

**creative-fillers:** continua preenchendo tabs; pode `softdepend` minimal-items. Não substitui o registro de blocos.

### Política `air`

1. `orion:minimal-blocks` registra `air` no `plugin.Load` (antes do bootstrap de mundo).
2. Qualquer generator (incl. void builtin / superflat plugin) declara `depend` `orion:minimal-blocks` **ou** o first-run garante o plugin na pasta e o host documenta fail-fast se `air` ausente no freeze.
3. Core **não** chama `RegisterBlock` para conteúdo.

## 5. Mudanças no core

- Esvaziar `RegisterFromBedrockStates` (método removido ou no-op documentado).
- Remover hashes hardcoded de conteúdo da allowlist Nature se existirem.
- Testes que assumiam dirt/grass nativos passam a carregar o plugin (ou fixtures de teste registram blocos).

## 6. Commits (exemplo)

1. `feat(plugins): add orion:minimal-blocks with six bedrock hashes`
2. `feat(plugins): add orion:minimal-items runtime id map`
3. `refactor(orion): remove RegisterFromBedrockStates native content`
4. `test: catalog empty without minimal-blocks; air present with plugin`
5. `docs(first-run): note minimal-blocks requirement` (completo na 30)

Sem `Co-authored-by`.

## 7. Acceptance tests

- [ ] Sem plugins: zero blocos de conteúdo (ou só o que a Api definir como impossível — preferir zero + fail claro).
- [ ] Com `minimal-blocks`: 6 ids resolvem; air presente antes do primeiro chunk.
- [ ] `orion:superflat` (fase 29) não compila/boot sem minimal-blocks.
- [ ] NuGet/CI template [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
