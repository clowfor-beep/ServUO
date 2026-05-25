#!/bin/bash
# restart.sh — safely restart ServUO inside Docker
# Usage: restart.sh [--skip-save]
#   --skip-save  Skip worldsave (deploy.sh already saved before calling this)
SKIP_SAVE=0
if [ "${1}" = "--skip-save" ]; then
    SKIP_SAVE=1
fi

echo "Stopping ServUO..."

# Send worldsave command before stopping (unless caller already did it)
if [ "$SKIP_SAVE" -eq 0 ] && screen -list 2>/dev/null | grep -q "servuo"; then
    echo "Saving world..."
    screen -S servuo -p 0 -X stuff "worldsave$(printf '\r')"
    sleep 5
    echo "World save complete."
fi

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
bash /home/servuo/start.sh
sleep 3

if ! pgrep -f "mono ServUO.exe" > /dev/null; then
    echo "ERROR: ServUO failed to start. Check /home/servuo/servuo.log"
    exit 1
fi

echo "Waiting for ServUO to finish loading..."

# Wait up to 3 minutes for the server to accept connections on port 2593
for i in $(seq 1 90); do
    sleep 2

    if ! pgrep -f "mono ServUO.exe" > /dev/null; then
        echo "ERROR: ServUO process died. Check the log: tail -30 /home/servuo/servuo.log"
        exit 1
    fi

    # Check if port 2593 is open and accepting connections
    if cat /proc/net/tcp 2>/dev/null | grep -q '00000000:0A21'; then
        echo ""
        echo "ServUO is online and ready."
        exit 0
    fi

    # Show latest log line so user can see what the server is doing
    LAST_LINE=$(strings /home/servuo/servuo.log 2>/dev/null | grep -v '^$' | tail -1)
    printf "\r  [%3ds] %-80s" "$((i*2))" "${LAST_LINE:0:80}"
done

echo ""
echo "ServUO is still loading after 3 minutes. Check with:"
echo "  strings /home/servuo/servuo.log | tail -5"
