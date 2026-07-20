# Phase 0 — Vision: Orion as a minimal engine

**Status:** `spec`  
**Language twin:** [`../../pt_br/plugins/00-vision-minimal-engine.md`](../../pt_br/plugins/00-vision-minimal-engine.md)

## 1. Goal

Define OrionServer as a **minimal Bedrock engine**: enough to accept connections, stream chunks, tick scheduling, and expose stable extension points — while **almost all gameplay and content** arrives from opt-in third-party plugins.

## 2. Non-goals

- Cloning vanilla BDS feature-complete survival.
- Shipping a large default plugin set; core ships empty of third-party plugins (`Plugins.Enabled` defaults to `false`).
- Magical conflict resolution when two plugins mutate the same resource.
- Native AOT host that still loads arbitrary plugin DLLs (incompatible with dynamic ALC).

## 3. Public API sketch

Phase 0 is conceptual. The only “API” is the boundary checklist:

```csharp
// Core owns (non-exhaustive)
// - RakNet + packet codec/framing
// - Session / Player lifecycle plumbing
// - World provider, chunk send, area scheduling
// - Protocol registries needed for a joinable world
// - PluginHost bootstrap + PluginContracts types

// Plugins own (over time)
// - Extra items/blocks/recipes/generators
// - Gameplay rules, combat, projectiles, economy, minigames
// - Commands, permissions policies, custom UIs
// - Packet-level features until a high-level API exists
```

## 4. Boot / runtime sequence

1. Process starts → load `server.json`.
2. If `Plugins.Enabled`, resolve and load plugin assemblies (Phase 1–2).
3. Core builds world + network (existing `ServerHost.Bootstrap`).
4. Plugins enable and register into events/registries (Phases 3–4).
5. Players join; runtime uses events, services, messaging, optional packet hooks.

## 5. File touch list (today’s anchors)

| Path | Role |
|------|------|
| [`src/Server/Program.cs`](../../../src/Server/Program.cs) | Boot order |
| [`src/Orion/ServerHost.cs`](../../../src/Orion/ServerHost.cs) | Core bootstrap |
| [`src/Orion/Server.cs`](../../../src/Orion/Server.cs) | Event bus stub (`On`/`Emit`) |
| [`src/Orion/Plugins/`](../../../src/Orion/Plugins/) | Current stub host |
| [`config/server.json`](../../../config/server.json) | `Plugins` section |
| [`plugins/MinimalInventoryItems/`](../../../plugins/MinimalInventoryItems/) | Sample opt-in plugin |
| [`plugins/VanillaAttributes/`](../../../plugins/VanillaAttributes/) | Vanilla health/hunger + attribute API (opt-in; refs Orion) |
| [`plugins/VanillaInventory/`](../../../plugins/VanillaInventory/) | Player inventory + ISR (opt-in; `depend` VanillaContainers) |
| [`plugins/VanillaContainers/`](../../../plugins/VanillaContainers/) | Storage/UI runtime (`provides` orion:containers) |
| [`plugins/VanillaContainerBlocks/`](../../../plugins/VanillaContainerBlocks/) | Chest/barrel (`depend` VanillaContainers + VanillaInventory) |
| [`plugins/VanillaBuilding/`](../../../plugins/VanillaBuilding/) | Block place / use-on-block (`softdepend` VanillaInventory) |
| [`plugins/VanillaMining/`](../../../plugins/VanillaMining/) | Block crack / destroy (`softdepend` VanillaInventory) |

## 6. Acceptance tests (definition of done for the vision)

- Docs state a clear core vs plugin split reviewed against [architecture philosophy](../architecture-philosophy.md).
- First-run docs tell operators that plugins are optional and empty creative tabs warn intentionally.
- No requirement that a production server load any sample plugin.

## 7. Migration notes from current stub

Today Orion already leans DIY: curated Nature blocks, no auto-loaded plugins, sample creative fillers only. Phase 0 freezes that intent and names the expansion path (contracts → lifecycle → events → registries → soft messaging → packet hooks).

## 8. Status

`spec` — no code change required for this phase alone.

## Core vs plugins (detail)

### Stays in core

| Area | Why |
|------|-----|
| UDP / RakNet, compression, encryption handshake | Shared substrate |
| Packet serialize/deserialize for known `PacketId`s | Protocol correctness |
| Session attach/detach, login → spawn pipeline | Must be reliable |
| Chunk generation hooks + streaming | Scale/threading model |
| Area / session schedulers | Affinity rules for events |
| Minimal item/block tables for a joinable void/flat world | Client must not brick |
| Plugin loader + contracts + services/messenger shells | Platform surface |

### Moves to plugins (over phases)

| Area | Notes |
|------|-------|
| Creative tab fillers | Already sample-plugin territory |
| Custom items/blocks/recipes | Phase 4 registries |
| Generators / dimensions content packs | Phase 4; Superflat → [29](29-worldgen-superflat-plugin.md) |
| Entity/Block/Item/Player traits + 6 native blocks | [22](22-vanilla-extraction-overview.md)–[28](28-minimal-content-and-empty-core.md) |
| Combat, projectiles, vehicles | Prefer events; packet hooks until APIs exist |
| Economy, permissions packs, minigames | Services + messaging |
| Proxy/plugin messaging channels | Phase 5–6 as needed |

## Analogies

| Project | Takeaway for Orion |
|---------|-------------------|
| **Paper / Bukkit** | Plugin jar + lifecycle + soft/hard depends + ServicesManager |
| **Endstone** | Bedrock server with rich events **and** packet receive/send hooks |
| **PocketMine** | `plugin.yml` depend/softdepend; `DataPacketReceiveEvent` escape hatch |
| **Serenity / the-aether** | `onInitialize` vs `onWorldInitialize`; register types into world palettes |
| **Fabric** | Explicit registries; content is registered, not patched into binaries |

Orion’s shape: **Paper-like plugin platform** + **Serenity-like content registration hooks** + **Endstone-like packet escape hatch**, on a **C# / McMaster** loader.
