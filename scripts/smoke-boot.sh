#!/usr/bin/env bash
# Smoke-boot a published OrionServer linux-x64 directory (empty plugins).
set -euo pipefail

PUBLISH_DIR="${1:-}"
if [[ -z "${PUBLISH_DIR}" ]]; then
  echo "usage: $0 <publish-dir>" >&2
  exit 2
fi

PUBLISH_DIR="$(cd "${PUBLISH_DIR}" && pwd)"
BIN="${PUBLISH_DIR}/OrionServer"
CONFIG_SRC="${PUBLISH_DIR}/config"

if [[ ! -f "${BIN}" ]]; then
  echo "OrionServer binary not found at ${BIN}" >&2
  exit 1
fi
chmod +x "${BIN}" || true

if [[ ! -d "${CONFIG_SRC}" ]]; then
  echo "config directory not found at ${CONFIG_SRC}" >&2
  exit 1
fi

WORKDIR="$(mktemp -d -t orion-smoke-XXXXXX)"
SERVER_PID=""
cleanup() {
  if [[ -n "${SERVER_PID}" ]] && kill -0 "${SERVER_PID}" 2>/dev/null; then
    kill -INT "${SERVER_PID}" 2>/dev/null || kill -TERM "${SERVER_PID}" 2>/dev/null || true
    for _ in $(seq 1 50); do
      kill -0 "${SERVER_PID}" 2>/dev/null || break
      sleep 0.1
    done
    kill -KILL "${SERVER_PID}" 2>/dev/null || true
    wait "${SERVER_PID}" 2>/dev/null || true
  fi
  rm -rf "${WORKDIR}"
}
trap cleanup EXIT

mkdir -p "${WORKDIR}/config" "${WORKDIR}/plugins"
cp -a "${CONFIG_SRC}/." "${WORKDIR}/config/"

PORT=$((19000 + RANDOM % 1000))
PORT6=$((PORT + 1))
python3 - "${WORKDIR}/config/server.json" "${PORT}" "${PORT6}" <<'PY'
import json, sys
path, port, port6 = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
with open(path) as f:
    cfg = json.load(f)
cfg["Server"]["Raknet"]["PortIPV4"] = port
cfg["Server"]["Raknet"]["PortIPV6"] = port6
cfg["Server"]["Raknet"]["ValidatePort"] = False
cfg["Server"]["Raknet"]["Address"] = "127.0.0.1"
with open(path, "w") as f:
    json.dump(cfg, f, indent=2)
    f.write("\n")
PY

LOG="${WORKDIR}/server.log"
cd "${WORKDIR}"
"${BIN}" "${WORKDIR}/config/server.json" >"${LOG}" 2>&1 &
SERVER_PID=$!

deadline=$((SECONDS + 30))
booted=0
while (( SECONDS < deadline )); do
  if ! kill -0 "${SERVER_PID}" 2>/dev/null; then
    echo "OrionServer exited before listening. Log:" >&2
    cat "${LOG}" >&2 || true
    exit 1
  fi
  if grep -q "listening on" "${LOG}" 2>/dev/null; then
    booted=1
    break
  fi
  sleep 0.2
done

if (( booted != 1 )); then
  echo "Timed out waiting for 'listening on'. Log:" >&2
  cat "${LOG}" >&2 || true
  exit 1
fi

echo "Smoke boot OK (port ${PORT}). Stopping…"
kill -TERM "${SERVER_PID}" 2>/dev/null || kill -INT "${SERVER_PID}" 2>/dev/null || true

stop_deadline=$((SECONDS + 20))
while kill -0 "${SERVER_PID}" 2>/dev/null && (( SECONDS < stop_deadline )); do
  sleep 0.2
done

if kill -0 "${SERVER_PID}" 2>/dev/null; then
  echo "Server did not stop after SIGTERM; forcing." >&2
  kill -KILL "${SERVER_PID}" 2>/dev/null || true
  wait "${SERVER_PID}" 2>/dev/null || true
  SERVER_PID=""
  exit 1
fi

wait "${SERVER_PID}" 2>/dev/null || true
SERVER_PID=""

if ! grep -q "stopped" "${LOG}"; then
  echo "Warning: 'stopped' not found in log (process exited)." >&2
  cat "${LOG}" >&2 || true
fi

echo "Smoke boot passed."
