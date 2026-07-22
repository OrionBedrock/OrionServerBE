# Fase 29 — Worldgen: plugin Superflat e void no core

**Status:** `implemented`  
**Language twin:** [`../../en_us/plugins/29-worldgen-superflat-plugin.md`](../../en_us/plugins/29-worldgen-superflat-plugin.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [23](23-extraction-sdk-prerequisites.md)  
**Follow-up:** [28](28-minimal-content-and-empty-core.md) (`orion:minimal-items` — implemented)

## 1. Goal

1. Remover `SuperFlatGenerator` dos builtins de [`GeneratorFactory`](../../../src/World/Generation/GeneratorFactory.cs).
2. Publicar `orion:superflat` que registra o generator `superflat` via `IGeneratorRegistry` em **`Load`** (antes do freeze).
3. Core mantém apenas builtin **`void`** (e fallback desconhecido → void).
4. Geração permanece **síncrona** via `Orion.Api.Worldgen.WorldGeneratorBase`.

## 2. Non-goals

- Geradores terrain avançados / biomes.
- Pregen paralelo obrigatório.

## 3. Plugin

| Campo | Valor |
|-------|--------|
| id | `orion:superflat` |
| PackageId | `Orion.Plugins.Superflat` |
| Repo | `OrionBedrock/orion-superflat` |
| provides | `orion:superflat` |
| depend | `["orion:minimal-items"]` |
| API | `WorldGeneratorBase` + `IChunkGenerationContext` |

Camadas (baseY −64): bedrock → 3× dirt → grass_block.

Registro: `context.Registries.Generators.Register("superflat", typeof(SuperFlatWorldGenerator))` em `Load`.

## 4. Mudanças no core

```csharp
// GeneratorFactory builtins
"void" => new VoidGenerator(),
_ => new VoidGenerator()
```

- Plugin types subclass `WorldGeneratorBase`; host wrapa com `ApiGeneratorAdapter`.
- `SuperFlatGenerator.cs` removido do assembly World.

## 5. Acceptance tests

- [x] Sem plugin: `Create("superflat")` → `VoidGenerator`.
- [x] Com registro Api: camadas iguais ao comportamento antigo.
- [ ] `depend` `orion:minimal-items` (after plugin ships).
- [x] Plugin CI: PackageReferenceTests + smoke boot.

## 6. Status

`implemented` (2026-07-21). Minimal-blocks dependency deferred to fase 28.
