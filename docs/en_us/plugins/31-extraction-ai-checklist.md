# Phase 31 — AI implementation checklist — Vanilla extraction

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/31-extraction-ai-checklist.md`](../../pt_br/plugins/31-extraction-ai-checklist.md)  
**Depends on:** [22](22-vanilla-extraction-overview.md)–[30](30-first-run-and-boot-order.md), [18](18-sdk-ai-implementation-checklist.md)

## 1. Goal

Executable order for an AI (or human) to implement extraction **without** skipping SDK contracts and **without** `Co-authored-by` commits.

## 2. Global prohibitions

- `Co-authored-by` / automatic coauthor trailers.
- `ProjectReference` to `Orion.csproj` on any plugin’s final merge.
- Publishing `Orion.dll` to NuGet.
- Leaving content in `RegisterFromBedrockStates` after phase 28.
- Keeping `superflat` as a builtin after phase 29.
- Creating `orion:entity-damage` parallel to `orion:attributes`.
- Requiring an async `Generator.Generate` API.

## 3. Gate: SDK before clean extraction

Complete (or equivalent) from [18](18-sdk-ai-implementation-checklist.md):

1. Packable `Orion.Api` + `Orion.Gameplay.Api` projects.
2. Trait registries + detail types in Api.
3. SharedAssemblies without sharing Orion implementation.
4. External sample builds with NuGet only.

Only then treat [24](24-entity-mechanics-plugins.md)–[29](29-worldgen-superflat-plugin.md) as “final state”. Spikes with `// TODO SDK` only on WIP branches.

## 4. Execution order

```text
23 (gaps closed / SDK)
 → 24 entity mechanics plugins
 → 25 block mechanics plugins
 → 26 item mechanics plugins
 → 27 player mechanics plugins
 → 28 minimal-blocks (+ items)
 → 29 superflat plugin + remove builtin
 → 30 first-run void + docs + build-plugins order
 → 17 dogfood: zero Orion.csproj refs on first-party
```

Parallel allowed: 25 ∥ 26 ∥ 27 after 23; **28 before 29**.

## 5. Per new plugin (repeat)

Mechanical checklist:

1. Create `Plugins-Orion/orion:<id>/` + `OrionBedrock/orion-…` repo.
2. plugin.json v2, `.csproj`, `Directory.Build.props`, `src/`.
3. `PackageId` `Orion.Plugins.*`; `ci.yml` / `publish.yml` (paths + auto-bump + OrionBedrock gate).
4. `development` / `main` branches.
5. Manifest `depend` / `softdepend` / `provides` per phase.
6. Granular Conventional Commits.
7. Push fork + `OrionBedrock`.
8. Only then PR `refactor(orion): remove … from core`.

## 6. Commit message examples

```
feat(plugins): add orion:entity-gravity scaffold
feat(plugins): register EntityGravityTrait on Load
ci: add build and NuGet publish workflows
refactor(orion): remove EntityGravityTrait from core
test: boot with entity-gravity plugin
chore(first-run): default generator to void
```

## 7. Series 22–30 global DoD

- [ ] Entity/Block/Item/Player traits listed in 24–27 no longer live in Orion.dll.
- [ ] No content in `RegisterFromBedrockStates`.
- [ ] `GeneratorFactory` without superflat builtin; void default.
- [ ] First-run / assets/ci use `generator: void`.
- [ ] All new plugins match NuGet/CI pattern.
- [ ] First-party has no Orion `ProjectReference` (dogfood [17](17-sdk-vanilla-dogfood.md)).
- [ ] Game.Tests + development CI green.
- [ ] pt_br + en_us docs Status updated when each phase becomes `implemented`.

## 8. Status

`spec` — use this page as the runbook; check items off as PRs merge.
