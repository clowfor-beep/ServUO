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
# Record current log line count before issuing worldsave — used to detect
# the "World: Save" completion message that ServUO writes to its log.
LOG_LINES=$(docker exec servuo bash -c "strings /home/servuo/servuo.log 2>/dev/null | wc -l" || echo 0)
# -p 0 targets the first screen window explicitly (matches restart.sh)
docker exec servuo bash -c "screen -S servuo -p 0 -X stuff 'worldsave\015'" 2>/dev/null || true

# Poll the log for ServUO's worldsave completion message. Timeout after 90s.
echo "Waiting for world save to complete..."
SAVE_DONE=0
for i in $(seq 1 18); do
    sleep 5
    FOUND=$(docker exec servuo bash -c "strings /home/servuo/servuo.log 2>/dev/null | tail -n +${LOG_LINES} | grep -ic 'world.*sav'" 2>/dev/null || true)
    FOUND="${FOUND//[^0-9]/}"
    if [ "${FOUND:-0}" -gt 0 ] 2>/dev/null; then
        echo "World save detected in log after $((i * 5))s."
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
