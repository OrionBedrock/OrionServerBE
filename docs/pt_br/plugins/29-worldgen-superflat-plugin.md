# Fase 29 — Worldgen: plugin Superflat e void no core

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/29-worldgen-superflat-plugin.md`](../../en_us/plugins/29-worldgen-superflat-plugin.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md), [28](28-minimal-content-and-empty-core.md)

## 1. Goal

1. Remover `SuperFlatGenerator` dos builtins de [`GeneratorFactory`](../../../src/World/Generation/GeneratorFactory.cs).
2. Publicar `orion:superflat` que registra o generator `superflat` via `IGeneratorRegistry` em `Load`/`OnEnable`.
3. Core mantém apenas builtin **`void`** (e fallback desconhecido → void).
4. Confirmar: geração permanece **síncrona** (`Generate`); sem requisito de API async.

## 2. Non-goals

- Geradores terrain avançados / biomes.
- Pregen paralelo obrigatório (AreaShards já multithreadam o mundo; pregen bootstrap pode permanecer sync).
- Mudar first-run templates (fase [30](30-first-run-and-boot-order.md) aplica `generator: void`).

## 3. Plugin

| Campo | Valor |
|-------|--------|
| id | `orion:superflat` |
| PackageId | `Orion.Plugins.Superflat` |
| Repo | `OrionBedrock/orion-superflat` |
| provides | `orion:superflat` |
| depend | **`orion:minimal-blocks`** `[1.0,99.0]` |
| Origem | [`SuperFlatGenerator.cs`](../../../src/World/Generation/SuperFlatGenerator.cs) |

Camadas atuais (baseY −64): bedrock → 3× dirt → grass — exigem ids do minimal-blocks.

Implementação: classe generator no plugin (mesmo contrato `Generator` exposto via Api / shared type), `Registries.Generators.Register("superflat", ...)`.

## 4. Mudanças no core

```csharp
// GeneratorFactory builtins (alvo)
"void" => new VoidGenerator(),
_ => new VoidGenerator()
// sem case "superflat"
```

- Mover arquivo `SuperFlatGenerator.cs` para o plugin (ou reimplementar contra Api).
- Defaults de config (`OrionConfig` / assets): tratados na fase 30; código default string → `"void"`.

## 5. Threading / async (esclarecimento)

| Camada | Comportamento hoje | Exigência desta fase |
|--------|--------------------|----------------------|
| `Generator.Generate` | Sync | Sync |
| `ChunkPregenerator` | Loop sync no bootstrap | Sync ok |
| Area workers | Multithread mundo | Fora do generator |

Se no futuro um plugin precisar pregen paralelo, estender Api (fase 23 opcional) — **não bloqueia** 29.

## 6. Commits (exemplo)

1. `feat(plugins): add orion:superflat generator plugin`
2. `refactor(world): remove superflat builtin from GeneratorFactory`
3. `chore(config): default generator string to void` (pode ser o mesmo PR que 30)
4. `test: superflat loads only with plugin; void without`

Sem `Co-authored-by`.

## 7. Acceptance tests

- [ ] Sem plugin: `generator: superflat` na config falha ou cai em void **documentado**; preferir erro explícito se identifier desconhecido após freeze.
- [ ] Com plugin + minimal-blocks: superflat produz camadas iguais às atuais.
- [ ] `depend` minimal-blocks imped boot se blocos ausentes.
- [ ] NuGet/CI template [22](22-vanilla-extraction-overview.md) §8.

## 8. Status

`spec`
