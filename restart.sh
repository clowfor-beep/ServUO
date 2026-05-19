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
sleep 5

# Confirm
if pgrep -f "mono ServUO.exe" > /dev/null; then
    echo "ServUO is running."
else
    echo "ERROR: ServUO failed to start. Check /home/servuo/servuo.log"
fi
