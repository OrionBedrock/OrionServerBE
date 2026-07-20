# Fase 23 — Pré-requisitos de SDK para extração

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/23-extraction-sdk-prerequisites.md`](../../en_us/plugins/23-extraction-sdk-prerequisites.md)  
**Depende de:** [09](09-sdk-overview.md)–[15](15-sdk-protocol-escape.md), [22](22-vanilla-extraction-overview.md)  
**Bloqueia:** [24](24-entity-mechanics-plugins.md)–[29](29-worldgen-superflat-plugin.md) (estado final sem `Orion.csproj`)

## 1. Goal

Listar **gaps de superfície** em `Orion.Api` / `Orion.Gameplay.Api` / contratos de registry que devem existir **antes** (ou em lockstep mínimo com) a extração de traits/conteúdo/worldgen para plugins, para que first-party e terceiros compilem só contra NuGet.

## 2. Non-goals

- Implementar o SDK nesta fase de doc (execução = train [18](18-sdk-ai-implementation-checklist.md)).
- Inventar API async de `Generator.Generate` (hoje síncrono; multithreading é AreaShard).
- Mover traits nesta fase.

## 3. Já existe (não reinventar)

| Capacidade | Onde |
|------------|------|
| Lifecycle + manifest v2 | PluginContracts / fases 1–2, 19 |
| `IGeneratorRegistry` → `GeneratorFactory.Register` | PluginHost facades |
| `RegisterPluginBlock` / item / creative / commands | Registries finos |
| Services + `IEntityHealthService` / building / mining / inventory | `Orion/Gameplay/*` (mover para Gameplay.Api) |
| Event bus + sinais Entity/Player | Core → catálogo Orion.Api.Events ([13](13-sdk-events-signals.md)) |
| Freeze de catálogo / generators | `NotifyCatalogLoaded` / `NotifyWorldBootstrapped` |

## 4. Gaps obrigatórios antes do move “limpo”

### 4.1 Orion.Api — shells e registries ricos ([11](11-sdk-orion-api-surface.md), [12](12-sdk-registries-traits.md))

| Gap | Por quê a extração precisa |
|-----|----------------------------|
| Facades `IEntity` / `IBlock` / `IItem` / `IPlayer` estáveis | Traits em plugins não podem referenciar tipos internos Orion |
| Trait registries públicos (`EntityTraits`, `BlockTraits`, `ItemTraits`, `PlayerTraits`) | Plugins registram traits sem `RegisterFromAssembly` no core |
| Tipos de detalhes (`*PlaceDetails`, `*BreakDetails`, move/spawn options, …) no pacote Api | Assinaturas de hooks nos traits migrados |
| `Trait` / `TraitOnTickDetails` no Api (não só no Orion.dll) | Base compartilhada McMaster |
| Escrita de bloco em chunk / resolução de permutação via Api | Minimal-blocks + superflat sem World internals |
| Catálogo de sinais em `Orion.Api.Events` | Hurt/die/spawn/place/break sem IVT |

### 4.2 Orion.Gameplay.Api ([14](14-sdk-gameplay-services.md))

| Gap | Por quê |
|-----|---------|
| Mover `IAttributesApi`, `IEntityHealthService`, `IPlayerHungerService`, building/mining/inventory interfaces para o pacote | Plugins de air-supply / attributes / building / mining dogfoodam o mesmo pacote |
| Documentar `provides` ↔ interface | Discovery estável pós-extração |

### 4.3 Worldgen / scheduling (documentar, não bloquear async)

| Item | Decisão |
|------|---------|
| Registro de generator | Já coberto por `IGeneratorRegistry` — expor tipagem estável no Api se ainda for facade interna |
| `Generate(x,z)` | Continua **síncrono** |
| Pregen paralelo via plugin | **Opcional futuro** — não bloqueia fases 28–29; se necessário depois, surface em Api (ex. job no area worker) |
| Tick de traits | Garantir que Entity/Block/Item/Player trait ticks continuam disparando após traits saírem do assembly Orion (bind via registry) |

### 4.4 McMaster SharedAssemblies ([10](10-sdk-packages-versioning.md))

Allowlist final deve incluir assemblies Api/Gameplay.Api/PluginContracts. **Remover** share de `typeof(Server)` / Orion implementação.

## 5. Política de transição (enquanto SDK incompleto)

| Etapa | Permitido | Proibido no merge final da extração |
|-------|-----------|-------------------------------------|
| Spike / WIP em branch | `ProjectReference` Orion temporário marcado `// TODO SDK` | Merge em `main` first-party ainda apontando Orion.dll |
| Após pack NuGet Api | Só PackageReference ExcludeAssets=runtime | InternalsVisibleTo para plugins |

Checklist de implementação do SDK: [18](18-sdk-ai-implementation-checklist.md). Extração de código: [31](31-extraction-ai-checklist.md) só após S1–S4 do SDK (ou equivalente).

## 6. Ordem de commits sugerida (SDK, não extração)

1. `feat(sdk): add Orion.Api project skeleton`
2. `feat(sdk): add Orion.Gameplay.Api and move gameplay interfaces`
3. `feat(sdk): expose trait registries and detail types`
4. `feat(host): SharedAssemblies allowlist for Api packages`
5. `docs(plugins): mark phase 23 gaps closed` (quando DoD abaixo ok)

Sem `Co-authored-by`.

## 7. Acceptance tests

- [ ] `Orion.Api` e `Orion.Gameplay.Api` existem como projetos packable.
- [ ] Interfaces de gameplay não vivem só em `Orion.dll`.
- [ ] Trait base + detail types compilam a partir do NuGet Api.
- [ ] Plugin sample externo (sem clone) registra um BlockTrait e um generator.
- [ ] Documentado: async gen **não** é requisito.

## 8. Status

`spec` — pré-requisito documental da extração.
