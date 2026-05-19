#!/bin/bash
# restart.sh — safely restart ServUO inside Docker
echo "Stopping ServUO..."

# Send worldsave command before stopping
if screen -list 2>/dev/null | grep -q "servuo"; then
    echo "Saving world..."
    screen -S servuo -p 0 -X stuff "worldsave$(printf '\r')"
    sleep 8
    echo "World saved."
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
/home/servuo/start.sh
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
        echo "ERROR: ServUO process died. Check: docker exec servuo tail -30 /home/servuo/servuo.log"
        exit 1
    fi

    # Check if port 2593 is open and accepting connections
    if ss -tlnp 2>/dev/null | grep -q ':2593' || netstat -tlnp 2>/dev/null | grep -q ':2593'; then
        echo ""
        echo "ServUO is online and ready."
        exit 0
    fi

    echo "  loading... ($((i*2))s)"
done

echo ""
echo "ServUO is running (still loading or high load). Check with:"
echo "  docker exec servuo ps aux | grep mono"
