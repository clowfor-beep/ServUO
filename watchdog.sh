#!/bin/bash
# watchdog.sh — monitor ServUO health and auto-restart if hung
# Run via cron every 5 minutes:
#   */5 * * * * bash /home/servuo/watchdog.sh >> /home/servuo/watchdog.log 2>&1

CONTAINER="servuo"
LOG="/home/servuo/servuo.log"
WATCHDOG_LOG="/home/servuo/watchdog.log"
MAX_SAVE_AGE=1200  # 20 minutes — server saves every 15 min, allow some slack

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

# Check if port 2593 is listening
if ! docker exec "$CONTAINER" bash -c "cat /proc/net/tcp | grep -q '0A21'"; then
    echo "[$(timestamp)] ERROR: Port 2593 not listening. Restarting..." >> "$WATCHDOG_LOG"
    docker exec "$CONTAINER" bash /home/servuo/restart.sh >> "$WATCHDOG_LOG" 2>&1
    exit 1
fi

# Check time since last world save in the log
LAST_SAVE=$(docker exec "$CONTAINER" bash -c "strings $LOG | grep 'Save finished' | tail -1")
if [ -z "$LAST_SAVE" ]; then
    echo "[$(timestamp)] WARNING: No world save found in log yet." >> "$WATCHDOG_LOG"
    exit 0
fi

# Extract the timestamp from the last save line and check its age
LAST_SAVE_TIME=$(echo "$LAST_SAVE" | grep -oP '^\d{2}:\d{2}:\d{2}')
if [ -n "$LAST_SAVE_TIME" ]; then
    NOW_SECS=$(date +%s)
    SAVE_SECS=$(date -d "$(date +%Y-%m-%d) $LAST_SAVE_TIME" +%s 2>/dev/null || echo 0)
    AGE=$((NOW_SECS - SAVE_SECS))

    # Handle midnight rollover
    if [ "$AGE" -lt 0 ]; then
        AGE=$((AGE + 86400))
    fi

    if [ "$AGE" -gt "$MAX_SAVE_AGE" ]; then
        echo "[$(timestamp)] ERROR: Last world save was ${AGE}s ago (>${MAX_SAVE_AGE}s). Server appears hung. Restarting..." >> "$WATCHDOG_LOG"
        docker exec "$CONTAINER" bash /home/servuo/restart.sh >> "$WATCHDOG_LOG" 2>&1
        exit 1
    fi
fi

echo "[$(timestamp)] OK: Server healthy. Last save: ${LAST_SAVE}" >> "$WATCHDOG_LOG"
