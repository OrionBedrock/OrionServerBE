# Phase 23 — SDK prerequisites for extraction

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/23-extraction-sdk-prerequisites.md`](../../pt_br/plugins/23-extraction-sdk-prerequisites.md)  
**Depends on:** [09](09-sdk-overview.md)–[15](15-sdk-protocol-escape.md), [22](22-vanilla-extraction-overview.md)  
**Blocks:** [24](24-entity-mechanics-plugins.md)–[29](29-worldgen-superflat-plugin.md) (final state without `Orion.csproj`)

## 1. Goal

List **surface gaps** in `Orion.Api` / `Orion.Gameplay.Api` / registry contracts that must exist **before** (or in minimal lockstep with) extracting traits/content/worldgen into plugins, so first-party and third-party authors compile against NuGet only.

## 2. Non-goals

- Implementing the SDK in this doc phase (execution = train [18](18-sdk-ai-implementation-checklist.md)).
- Inventing an async `Generator.Generate` API (sync today; multithreading is AreaShard).
- Moving traits in this phase.

## 3. Already exists (do not reinvent)

| Capability | Where |
|------------|-------|
| Lifecycle + manifest v2 | PluginContracts / phases 1–2, 19 |
| `IGeneratorRegistry` → `GeneratorFactory.Register` | PluginHost facades |
| `RegisterPluginBlock` / item / creative / commands | Thin registries |
| Services + health/building/mining/inventory interfaces | `Orion/Gameplay/*` (move into Gameplay.Api) |
| Event bus + Entity/Player signals | Core → Orion.Api.Events catalog ([13](13-sdk-events-signals.md)) |
| Catalog / generator freeze | `NotifyCatalogLoaded` / `NotifyWorldBootstrapped` |

## 4. Mandatory gaps before a “clean” move

### 4.1 Orion.Api — shells and rich registries ([11](11-sdk-orion-api-surface.md), [12](12-sdk-registries-traits.md))

| Gap | Why extraction needs it |
|-----|-------------------------|
| Stable `IEntity` / `IBlock` / `IItem` / `IPlayer` facades | Trait plugins must not reference Orion internals |
| Public trait registries (`EntityTraits`, `BlockTraits`, `ItemTraits`, `PlayerTraits`) | Plugins register traits without core `RegisterFromAssembly` |
| Detail types (`*PlaceDetails`, move/spawn options, …) in the Api package | Hook signatures on migrated traits |
| `Trait` / `TraitOnTickDetails` in Api (not only Orion.dll) | Shared McMaster base |
| Chunk block write / permutation resolve via Api | Minimal-blocks + superflat without World internals |
| Signal catalog in `Orion.Api.Events` | Hurt/die/spawn/place/break without IVT |

### 4.2 Orion.Gameplay.Api ([14](14-sdk-gameplay-services.md))

| Gap | Why |
|-----|-----|
| Move `IAttributesApi`, `IEntityHealthService`, hunger, building/mining/inventory interfaces into the package | Air-supply / attributes / building / mining dogfood the same package |
| Document `provides` ↔ interface | Stable discovery after extraction |

### 4.3 Worldgen / scheduling (document; do not block on async)

| Item | Decision |
|------|----------|
| Generator registration | Covered by `IGeneratorRegistry` — expose stable typing in Api if still an internal facade |
| `Generate(x,z)` | Remains **synchronous** |
| Parallel pregen via plugin | **Optional future** — does not block phases 28–29 |
| Trait ticks | Ensure Entity/Block/Item/Player trait ticks still fire after traits leave the Orion assembly (bind via registry) |

### 4.4 McMaster SharedAssemblies ([10](10-sdk-packages-versioning.md))

Final allowlist must include Api/Gameplay.Api/PluginContracts. **Remove** sharing `typeof(Server)` / Orion implementation.

## 5. Transition policy (while SDK incomplete)

| Stage | Allowed | Forbidden on final extraction merge |
|-------|---------|-------------------------------------|
| Spike / WIP branch | Temporary `ProjectReference` Orion marked `// TODO SDK` | `main` first-party still pointing at Orion.dll |
| After Api NuGet pack | PackageReference ExcludeAssets=runtime only | InternalsVisibleTo for plugins |

SDK implementation checklist: [18](18-sdk-ai-implementation-checklist.md). Code extraction: [31](31-extraction-ai-checklist.md) only after SDK S1–S4 (or equivalent).

## 6. Suggested commits (SDK, not extraction)

1. `feat(sdk): add Orion.Api project skeleton`
2. `feat(sdk): add Orion.Gameplay.Api and move gameplay interfaces`
3. `feat(sdk): expose trait registries and detail types`
4. `feat(host): SharedAssemblies allowlist for Api packages`
5. `docs(plugins): mark phase 23 gaps closed` (when DoD below is met)

No `Co-authored-by`.

## 7. Acceptance tests

- [ ] `Orion.Api` and `Orion.Gameplay.Api` exist as packable projects.
- [ ] Gameplay interfaces do not live only in `Orion.dll`.
- [ ] Trait base + detail types compile from the Api NuGet.
- [ ] External sample plugin (no clone) registers a BlockTrait and a generator.
- [ ] Documented: async gen is **not** a requirement.

## 8. Status

`spec` — documentary prerequisite for extraction.
