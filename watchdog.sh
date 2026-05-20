#!/bin/bash
# watchdog.sh — monitor ServUO health and auto-restart if hung
# Run via cron every 5 minutes:
#   */5 * * * * bash /home/servuo/watchdog.sh >> /home/servuo/watchdog.log 2>&1

CONTAINER="servuo"
WATCHDOG_LOG="/home/servuo/watchdog.log"
SAVES_DIR="/home/servuo/Saves"
MAX_SAVE_AGE=1200  # 20 minutes — server saves every 15 min, allow some slack
STARTUP_GRACE=300  # 5 minutes grace period after server starts before checking saves

timestamp() { date '+%Y-%m-%d %H:%M:%S'; }

# Check if container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER}$"; then
    echo "[$(timestamp)] ERROR: Container ${CONTAINER} is not running." >> "$WATCHDOG_LOG"
    exit 1
fi

# Check if mono process is alive
if ! docker exec "$CONTAINER" bash -c "pgrep -f mono > /dev/null 2>&1"; then
    echo "[$(timestamp)] ERROR: mono is not running. Restarting..." >> "$WATCHDOG_LOG"
    docker exec "$CONTAINER" bash /home/servuo/restart.sh >> "$WATCHDOG_LOG" 2>&1
    exit 1
fi

# Check if port 2593 is listening (state 0A = LISTEN, local addr 00000000 = all interfaces)
if ! docker exec "$CONTAINER" bash -c "cat /proc/net/tcp | grep -q '00000000:0A21'"; then
    echo "[$(timestamp)] WARNING: Port 2593 not yet listening — server may still be loading." >> "$WATCHDOG_LOG"
    exit 0
fi

# Check how long ago mono started
MONO_PID=$(docker exec "$CONTAINER" bash -c "pgrep -f mono | head -1")
MONO_START=$(docker exec "$CONTAINER" bash -c "stat -c %Y /proc/${MONO_PID}/exe 2>/dev/null || echo 0")
NOW_SECS=$(date +%s)
UPTIME=$((NOW_SECS - MONO_START))

if [ "$UPTIME" -lt "$STARTUP_GRACE" ]; then
    echo "[$(timestamp)] OK: Server just started (${UPTIME}s ago), skipping save check." >> "$WATCHDOG_LOG"
    exit 0
fi

# Check time since last world save via Saves directory modification time
SAVE_MTIME=$(stat -c %Y "$SAVES_DIR" 2>/dev/null || echo 0)
if [ "$SAVE_MTIME" -eq 0 ]; then
    echo "[$(timestamp)] WARNING: Cannot read Saves directory." >> "$WATCHDOG_LOG"
    exit 0
fi

SAVE_AGE=$((NOW_SECS - SAVE_MTIME))

if [ "$SAVE_AGE" -gt "$MAX_SAVE_AGE" ]; then
    echo "[$(timestamp)] ERROR: Last world save was ${SAVE_AGE}s ago (>${MAX_SAVE_AGE}s). Server appears hung. Restarting..." >> "$WATCHDOG_LOG"
    docker exec "$CONTAINER" bash /home/servuo/restart.sh >> "$WATCHDOG_LOG" 2>&1
    exit 1
fi

echo "[$(timestamp)] OK: Server healthy. Last save ${SAVE_AGE}s ago." >> "$WATCHDOG_LOG"
