#!/bin/bash
# restart.sh — safely restart ServUO inside Docker
echo "Stopping ServUO..."

# Kill the mono process gracefully first
pkill -f "mono ServUO.exe" 2>/dev/null

# Wait for it to fully die (up to 30 seconds)
for i in $(seq 1 30); do
    if ! pgrep -f "mono ServUO.exe" > /dev/null; then
        echo "ServUO stopped."
        break
    fi
    echo "  waiting... ($i)"
    sleep 1
done

# Force kill if still alive
if pgrep -f "mono ServUO.exe" > /dev/null; then
    echo "Force killing..."
    pkill -9 -f "mono ServUO.exe" 2>/dev/null
    sleep 2
fi

# Clean up any dead screen sessions
screen -wipe > /dev/null 2>&1

echo "Starting ServUO..."
/home/servuo/start.sh
sleep 3

if ! pgrep -f "mono ServUO.exe" > /dev/null; then
    echo "ERROR: ServUO failed to start. Check /home/servuo/servuo.log"
    exit 1
fi

echo "Compiling scripts — please wait..."

# Tail the log until we see the server is ready (up to 3 minutes)
LOG=/home/servuo/servuo.log
LOGSIZE=$(wc -c < "$LOG" 2>/dev/null || echo 0)

for i in $(seq 1 180); do
    # Read only new lines since the restart
    NEWLINES=$(tail -c +$LOGSIZE "$LOG" 2>/dev/null | strings)

    if echo "$NEWLINES" | grep -q "Scripts: Compilation"; then
        echo "  [compiling scripts...]"
    fi

    if echo "$NEWLINES" | grep -q "World: Loading"; then
        echo "  [loading world...]"
    fi

    if echo "$NEWLINES" | grep -q "Listening on\|Server is now online"; then
        echo ""
        echo "ServUO is online and ready."
        exit 0
    fi

    if echo "$NEWLINES" | grep -qi "error\|exception" && echo "$NEWLINES" | grep -qi "scripts"; then
        echo ""
        echo "WARNING: Script errors detected — check the log:"
        tail -c +$LOGSIZE "$LOG" | strings | grep -i "error" | head -5
    fi

    sleep 1
done

echo "Timed out waiting — ServUO may still be starting. Check with:"
echo "  docker exec servuo tail -f /home/servuo/servuo.log"
