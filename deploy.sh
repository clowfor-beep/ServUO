#!/bin/bash
# deploy.sh — pull latest, build Scripts.dll, update website, restart ServUO
# Run this on the HOST (not inside the Docker container)
set -e

cd /home/servuo

echo "Pulling latest from git..."
git pull --no-edit

echo "Building Server.dll..."
dotnet build Server/Server.csproj -c Release --nologo -v minimal
cp Server/bin/Release/Server.dll /home/servuo/Server.dll
echo "Server.dll built."

echo "Building Scripts.dll..."
dotnet build Scripts/Scripts.csproj -c Release --nologo -v minimal
cp Scripts/bin/Release/Scripts.dll /home/servuo/Scripts.dll
echo "Scripts.dll built."

echo "Deploying website..."
cp website/index.html /var/www/html/index.html
cp website/submit.php /var/www/html/submit.php
cp website/admin.php /var/www/html/admin.php
cp website/update-status.php /var/www/html/update-status.php

chmod +x /home/servuo/restart.sh 2>/dev/null || true

echo "Saving world before restart..."
# Touch a reference file inside the container right before the save command —
# avoids cross-mount timestamp skew when comparing against Scripts.dll on the host.
docker exec servuo bash -c "touch /tmp/save_ref"
# \015 = carriage return — more reliable than $(printf '\r') through docker exec
docker exec servuo bash -c "screen -S servuo -X stuff 'worldsave\015'" 2>/dev/null || true

# Wait for save to complete — poll the Saves directory for recent changes.
# World saves can take 30-60s on a loaded server. Timeout after 90s.
echo "Waiting for world save to complete..."
SAVE_DONE=0
for i in $(seq 1 18); do
    sleep 5
    NEWEST=$(docker exec servuo bash -c "find /home/servuo/Saves -name '*.bin' -newer /tmp/save_ref 2>/dev/null | head -1")
    if [ -n "$NEWEST" ]; then
        echo "World save detected after $((i * 5))s."
        SAVE_DONE=1
        break
    fi
    echo "  ...waiting ($((i * 5))s)"
done
if [ "$SAVE_DONE" -eq 0 ]; then
    echo "WARNING: World save not detected within 90s — continuing anyway."
fi

echo "Applying prod config..."
cp Config/env/prod/Server.cfg Config/Server.cfg
cp Config/env/prod/DataPath.cfg Config/DataPath.cfg

echo "Restarting ServUO..."
docker exec servuo bash /home/servuo/restart.sh

echo "Deploy complete."
