# Fase 30 — First-run, void e ordem de boot

**Status:** `spec`  
**Language twin:** [`../../en_us/plugins/30-first-run-and-boot-order.md`](../../en_us/plugins/30-first-run-and-boot-order.md)  
**Depende de:** [22](22-vanilla-extraction-overview.md), [28](28-minimal-content-and-empty-core.md), [29](29-worldgen-superflat-plugin.md)

## 1. Goal

Atualizar bootstrap do operador (`scripts/first-run.sh`, [`assets/first-run/`](../../../assets/first-run/), [`assets/ci/`](../../../assets/ci/)) e documentar o **conjunto mínimo de plugins** para um servidor jogável pós-extração, com mundo default **`void`**.

## 2. Non-goals

- Empacotar todos os plugins first-party no zip de release (lista recomendada ≠ obrigatória).
- Habilitar `Plugins.Enabled: false` como default de produção (mantém opt-in documentado; first-run pode sugerir `true` para dogfood).

## 3. Mudanças de config

| Arquivo | Campo | De | Para |
|---------|-------|-----|------|
| `assets/first-run/server.json` | `dimensions[].generator` | `superflat` | **`void`** |
| `assets/ci/server.json` | idem | (alinhar) | **`void`** |
| Default em código `OrionConfig` se existir | generator | `superflat` | **`void`** |
| `docs/*/first-run.md` | texto | menciona superflat | void + como habilitar superflat plugin |

Spawn Y em void: ajustar documentação (ex. manter `[0, -57, 0]` ou elevar — implementação escolhe valor seguro; doc first-run explica).

## 4. Conjunto recomendado (“survival mínimo”)

Ordem lógica de load (manifest deps fazem o resto):

1. `orion:minimal-blocks` (+ `orion:minimal-items` se separado)
2. `orion:entity-attributes` → `orion:attributes`
3. `orion:entity-gravity` / `collision` / `movement` (e air-supply, equipment conforme necessidade)
4. `orion:player-chunk-rendering` (**obrigatório** para jogar)
5. `orion:containers` → `orion:inventory` → `orion:block_containers`
6. `orion:building` / `orion:mining` (opt-in)
7. `orion:creative-fillers` (opt-in)
8. `orion:superflat` **somente** se a config usar `generator: superflat`

Void puro: itens 1–5 (e traits entity necessários ao player/item) bastam para entrar num mundo vazio.

## 5. `build-plugins.sh` / deploy

Atualizar script em `Plugins-Orion/` para incluir novos ids na ordem de deps. Deploy para `OrionServerBE/plugins/<id>/`.

## 6. Commits (exemplo)

1. `chore(first-run): default world generator to void`
2. `chore(ci-fixtures): align assets/ci server.json generator to void`
3. `docs: document minimal plugin set and superflat opt-in`
4. `chore(build-plugins): add new first-party plugin ids to build order`

Sem `Co-authored-by`.

## 7. Acceptance tests

- [ ] `first-run.sh` produz `generator: void`.
- [ ] CI fixtures usam void (Logger.Tests / Game.Tests ainda passam — ajustar fixtures se dependiam de camadas superflat).
- [ ] Doc first-run lista plugins e como ativar superflat.
- [ ] Boot void + minimal-blocks + chunk-rendering permite join.

## 8. Status

`spec`
