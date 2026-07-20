# Fase 0 — Visão: Orion como engine mínima

**Status:** `spec`  
**Twin:** [`../../en_us/plugins/00-vision-minimal-engine.md`](../../en_us/plugins/00-vision-minimal-engine.md)

## 1. Objetivo

Definir o OrionServer como uma **engine Bedrock mínima**: o suficiente para aceitar conexões, streamar chunks, agendar ticks e expor pontos de extensão estáveis — enquanto **quase todo gameplay e conteúdo** chega de plugins opt-in de terceiros.

## 2. Não-objetivos

- Clonar survival vanilla completa do BDS.
- Enviar um conjunto grande de plugins por padrão; o core não carrega plugins de terceiros (`Plugins.Enabled` default `false`).
- Resolução mágica de conflitos quando dois plugins mutam o mesmo recurso.
- Host Native AOT que ainda carregue DLLs arbitrárias (incompatível com ALC dinâmico).

## 3. Esboço de API pública

A fase 0 é conceitual. A “API” é o checklist de fronteira:

```csharp
// Core possui (não exaustivo)
// - RakNet + codec/framing de pacotes
// - Ciclo de sessão / Player
// - Provider de mundo, envio de chunks, scheduling de áreas
// - Registries de protocolo para um mundo joinável
// - Bootstrap PluginHost + tipos PluginContracts

// Plugins possuem (ao longo do tempo)
// - Itens/blocos/receitas/generators extras
// - Regras de gameplay, combate, projéteis, economia, minigames
// - Comandos, permissões, UIs custom
// - Features em nível de packet até existir API de alto nível
```

## 4. Sequência de boot / runtime

1. Processo inicia → carrega `server.json`.
2. Se `Plugins.Enabled`, resolve e carrega assemblies (Fases 1–2).
3. Core monta mundo + rede (`ServerHost.Bootstrap`).
4. Plugins habilitam e registram eventos/registries (Fases 3–4).
5. Jogadores entram; runtime usa events, services, messaging e packet hooks opcionais.

## 5. Arquivos a tocar (âncoras atuais)

| Path | Papel |
|------|------|
| [`src/Server/Program.cs`](../../../src/Server/Program.cs) | Ordem de boot |
| [`src/Orion/ServerHost.cs`](../../../src/Orion/ServerHost.cs) | Bootstrap do core |
| [`src/Orion/Server.cs`](../../../src/Orion/Server.cs) | Stub do event bus (`On`/`Emit`) |
| [`src/Orion/Plugins/`](../../../src/Orion/Plugins/) | Host stub atual |
| [`config/server.json`](../../../config/server.json) | Seção `Plugins` |
| [`plugins/MinimalInventoryItems/`](../../../plugins/MinimalInventoryItems/) | Sample opt-in |
| [`plugins/VanillaAttributes/`](../../../plugins/VanillaAttributes/) | Vida/fome vanilla + API de atributos (opt-in; refs Orion) |
| [`plugins/VanillaInventory/`](../../../plugins/VanillaInventory/) | Inventário do jogador + ISR (opt-in; `depend` VanillaContainers) |
| [`plugins/VanillaContainers/`](../../../plugins/VanillaContainers/) | Runtime de grades/UI (`provides` orion:containers) |
| [`plugins/VanillaContainerBlocks/`](../../../plugins/VanillaContainerBlocks/) | Baú/barril (`depend` VanillaContainers + VanillaInventory) |
| [`plugins/VanillaBuilding/`](../../../plugins/VanillaBuilding/) | Place de bloco / use-on-block (`softdepend` VanillaInventory) |
| [`plugins/VanillaMining/`](../../../plugins/VanillaMining/) | Crack / destroy de bloco (`softdepend` VanillaInventory) |

## 6. Testes de aceitação (definição de pronto da visão)

- Docs com split core vs plugin alinhado à [filosofia de arquitetura](../architecture-philosophy.md).
- First-run deixa claro que plugins são opcionais e abas criativas vazias avisam de propósito.
- Nenhum requisito de carregar o sample em produção.

## 7. Notas de migração do stub atual

O Orion já é DIY: Nature curada, sem auto-load de plugins, fillers só no sample. A fase 0 congela essa intenção e nomeia o caminho (contratos → lifecycle → eventos → registries → messaging soft → packet hooks).

## 8. Status

`spec` — sem mudança de código só por esta fase.

## Core vs plugins (detalhe)

### Fica no core

| Área | Por quê |
|------|---------|
| UDP / RakNet, compressão, handshake | Substrato compartilhado |
| Serialize/deserialize de `PacketId`s conhecidos | Correção de protocolo |
| Attach/detach de sessão, login → spawn | Precisa ser confiável |
| Hooks de geração + streaming de chunks | Modelo de escala/threads |
| Schedulers de área / sessão | Affinity dos eventos |
| Tabelas mínimas de item/bloco para mundo joinável | Cliente não pode quebrar |
| Loader + contratos + shells de services/messenger | Superfície de plataforma |

### Migra para plugins (nas fases)

| Área | Notas |
|------|-------|
| Fillers de creative tabs | Já é território do sample |
| Itens/blocos/receitas custom | Fase 4 |
| Generators / conteúdo de dimensões | Fase 4; Superflat → [29](29-worldgen-superflat-plugin.md) |
| Traits Entity/Block/Item/Player + 6 blocos nativos | [22](22-vanilla-extraction-overview.md)–[28](28-minimal-content-and-empty-core.md) |
| Combate, projéteis, veículos | Preferir eventos; packet hooks até haver API |
| Economia, packs de permissão, minigames | Services + messaging |
| Canais de proxy/plugin messaging | Fases 5–6 conforme necessário |

## Analogias

| Projeto | Lição para o Orion |
|---------|-------------------|
| **Paper / Bukkit** | Plugin + lifecycle + soft/hard depends + ServicesManager |
| **Endstone** | Servidor Bedrock com eventos ricos **e** hooks de packet |
| **PocketMine** | depend/softdepend; escape hatch `DataPacketReceiveEvent` |
| **Serenity / the-aether** | `onInitialize` vs `onWorldInitialize`; register em palettes |
| **Fabric** | Registries explícitos; conteúdo registrado, não patch binário |

Formato Orion: **plataforma estilo Paper** + **registro de conteúdo estilo Serenity** + **escape hatch de packets estilo Endstone**, com loader **C# / McMaster**.
