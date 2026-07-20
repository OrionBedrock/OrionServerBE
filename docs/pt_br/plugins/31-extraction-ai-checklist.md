# Fase 31 — Checklist de implementação (IA) — Extração Vanilla

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/31-extraction-ai-checklist.md`](../../en_us/plugins/31-extraction-ai-checklist.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md)–[30](30-first-run-and-boot-order.md), [18](18-sdk-ai-implementation-checklist.md)

## 1. Goal

Ordem executável para uma IA (ou humano) implementar a extração **sem** pular contratos SDK e **sem** commits com `Co-authored-by`.

## 2. Proibições globais

- `Co-authored-by` / trailers de coautoria automática.
- `ProjectReference` a `Orion.csproj` no merge final de qualquer plugin.
- Publicar `Orion.dll` no NuGet.
- Deixar `RegisterFromBedrockStates` com conteúdo após a fase 28.
- Manter `superflat` como builtin após a fase 29.
- Criar `orion:entity-damage` paralelo a `orion:attributes`.
- Exigir API async de `Generator.Generate`.

## 3. Gate: SDK antes da extração limpa

Complete (ou equivalente) em [18](18-sdk-ai-implementation-checklist.md):

1. Projetos `Orion.Api` + `Orion.Gameplay.Api` packable.
2. Trait registries + detail types no Api.
3. SharedAssemblies sem share da implementação Orion.
4. Sample externo compila só com NuGet.

Só então trate [24](24-entity-mechanics-plugins.md)–[29](29-worldgen-superflat-plugin.md) como “estado final”. Spikes com `// TODO SDK` só em branches WIP.

## 4. Ordem de execução

```text
23 (gaps fechados / SDK)
 → 24 entity mechanics plugins
 → 25 block mechanics plugins
 → 26 item mechanics plugins
 → 27 player mechanics plugins
 → 28 minimal-blocks (+ items)
 → 29 superflat plugin + remove builtin
 → 30 first-run void + docs + build-plugins order
 → 17 dogfood: zero Orion.csproj refs nos first-party
```

Paralelo permitido: 25 ∥ 26 ∥ 27 após 23; **28 antes de 29**.

## 5. Por plugin novo (repetir)

Checklist mecânico:

1. Criar pasta `Plugins-Orion/orion:<id>/` + repo `OrionBedrock/orion-…`.
2. `plugin.json` v2, `.csproj`, `Directory.Build.props`, `src/`.
3. `PackageId` `Orion.Plugins.*`; workflows `ci.yml` / `publish.yml` (paths + auto-bump + gate OrionBedrock).
4. Branches `development` / `main`.
5. Manifest `depend` / `softdepend` / `provides` conforme a fase.
6. Commits granulares Conventional Commits.
7. Push fork + `OrionBedrock`.
8. Só então PR `refactor(orion): remove … from core`.

## 6. Mensagens de commit (exemplos)

```
feat(plugins): add orion:entity-gravity scaffold
feat(plugins): register EntityGravityTrait on Load
ci: add build and NuGet publish workflows
refactor(orion): remove EntityGravityTrait from core
test: boot with entity-gravity plugin
chore(first-run): default generator to void
```

## 7. DoD global da série 22–30

- [ ] Traits Entity/Block/Item/Player listados nas fases 24–27 não existem no Orion.dll.
- [ ] Zero conteúdo em `RegisterFromBedrockStates`.
- [ ] `GeneratorFactory` sem builtin superflat; void default.
- [ ] First-run / assets/ci com `generator: void`.
- [ ] Todos os novos plugins no padrão NuGet/CI.
- [ ] First-party sem `ProjectReference` Orion (dogfood [17](17-sdk-vanilla-dogfood.md)).
- [ ] Game.Tests + CI development verdes.
- [ ] Docs pt_br + en_us com Status atualizado quando cada fase for `implemented`.

## 8. Status

`spec` — use esta página como runbook; marque itens conforme PRs mergearem.
