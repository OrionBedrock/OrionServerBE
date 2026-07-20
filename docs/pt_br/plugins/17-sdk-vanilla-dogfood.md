# Fase 17 — Dogfooding Vanilla\* (final)

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/17-sdk-vanilla-dogfood.md`](../../en_us/plugins/17-sdk-vanilla-dogfood.md)  
**Depende de:** [10](10-sdk-packages-versioning.md)–[15](15-sdk-protocol-escape.md)

## 1. Goal

Migrar todos os Vanilla\* para os **mesmos pacotes SDK finais** usados por terceiros: sem `ProjectReference` a `Orion.csproj`, sem `InternalsVisibleTo` de produção, tipos em Orion.Api / Gameplay.Api.

## 2. Non-goals

- Reescrever comportamento vanilla.
- Criar mais plugins Vanilla do que os existentes.

## 3. Ordem de migração

1. VanillaContainers  
2. VanillaInventory  
3. VanillaAttributes  
4. VanillaBuilding  
5. VanillaMining  
6. VanillaContainerBlocks  
7. Verificar MinimalInventoryItems  

## 4. Regras

- Plugins first-party **podem** ProjectReference projetos SDK (`Orion.Api`, `Orion.Gameplay.Api`, `PluginContracts`) e **outros plugins**; **não podem** ProjectReference `Orion.csproj`.
- Autores externos usam NuGet (ver [16](16-sdk-external-plugin-guide.md)).
- Remover IVT Vanilla de `Orion.csproj`; expor o que for necessário via API pública.
- Traits via `context.Registries.*Traits`; sinais em `Orion.Api.Events`.
- Protocol só como detalhe de implementação ([15](15-sdk-protocol-escape.md)).
- Remover share McMaster de `typeof(Server)`.

## 5. Acceptance tests

- Nenhum `ProjectReference` a Orion.csproj em `plugins/`.
- Nenhum InternalsVisibleTo Vanilla no Orion.
- Boot completo com Vanilla\*: inventário, place, mine, baú, HUD attributes.
- Sample deep externo carrega junto.
- Game.Tests verde.

## 6. Relação com a Extração Vanilla

Novos first-party (traits, conteúdo mínimo, superflat) nascem já no padrão SDK; a série [22](22-vanilla-extraction-overview.md)–[31](31-extraction-ai-checklist.md) move o que ainda está no core. Esta fase 17 dogfooda os **7 existentes** + qualquer plugin novo da extração até zero `Orion.csproj`.

## 7. Status

`spec` — **auditoria jul/2026:** parcial — 7 plugins migrados para ids `orion:*`, layout `src/`, manifest v2, externalizados em `Plugins-Orion/`; `InternalsVisibleTo` removido; **pendente** `PackageReference` NuGet e zero `ProjectReference` a `Orion.csproj`.
