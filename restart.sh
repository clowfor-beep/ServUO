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

# Wait up to 3 minutes for the server to stabilise
# ServUO is ready when the mono process drops below 20% CPU (world load complete)
for i in $(seq 1 90); do
    sleep 2
    CPU=$(ps aux | grep "mono ServUO" | grep -v grep | awk '{print $3}' | head -1)
    CPU_INT=${CPU%.*}

    if [ -z "$CPU_INT" ]; then
        echo "ERROR: ServUO process died. Check: docker exec servuo tail -30 /home/servuo/servuo.log"
        exit 1
    fi

    echo "  loading... (CPU: ${CPU}%)"

    if [ "$CPU_INT" -lt 5 ] 2>/dev/null; then
        echo ""
        echo "ServUO is online and ready."
        exit 0
    fi
done

echo ""
echo "ServUO is running (still loading or high load). Check with:"
echo "  docker exec servuo ps aux | grep mono"
