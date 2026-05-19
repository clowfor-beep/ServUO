#!/bin/bash
# deploy-test.sh — build Scripts.dll and restart the TEST server only
# Run this on the HOST (not inside the Docker container)
# Production server is NOT affected.
set -e

cd /home/servuo

echo "Pulling latest from git..."
git pull --no-edit

echo "Building Server.dll..."
dotnet build Server/Server.csproj -c Release --nologo -v minimal
cp Server/bin/Release/Server.dll /home/servuo-test/Server.dll
echo "Server.dll built."

echo "Building Scripts.dll..."
dotnet build Scripts/Scripts.csproj -c Release --nologo -v minimal
cp Scripts/bin/Release/Scripts.dll /home/servuo-test/Scripts.dll
echo "Scripts.dll built."

echo "Applying test config..."
cp Config/env/test/Server.cfg /home/servuo-test/Config/Server.cfg
cp Config/env/test/DataPath.cfg /home/servuo-test/Config/DataPath.cfg

echo "Killing existing test server..."
docker exec servuo-test bash -c "pkill -9 -f mono 2>/dev/null; sleep 2" 2>/dev/null || true

echo "Starting TEST server (port 2594)..."
docker exec -d servuo-test bash -c "cd /home/servuo && mono ServUO.exe -noconsole < /dev/null >> /home/servuo/servuo.log 2>&1"

echo "Waiting for TEST server to finish loading..."
for i in $(seq 1 90); do
    sleep 2
    if ! docker exec servuo-test bash -c "pgrep -f mono > /dev/null 2>&1"; then
        echo "ERROR: TEST server process died. Check: docker exec servuo-test bash -c 'tail -30 /home/servuo/servuo.log'"
        exit 1
    fi
    if docker exec servuo-test bash -c "cat /proc/net/tcp 2>/dev/null | grep -q '0A22'"; then
        echo ""
        echo "TEST server is online and ready on port 2594."
        exit 0
    fi
    echo "  loading... ($((i*2))s)"
done

echo "TEST server is running but still loading. Check with:"
echo "  docker exec servuo-test bash -c 'cat /proc/net/tcp'"

echo "Deploy-test complete. Production server untouched."
