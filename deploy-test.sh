#!/bin/bash
# deploy-test.sh — build Scripts.dll and restart the TEST server only
# Run this on the HOST (not inside the Docker container)
# Production server is NOT affected.
set -e

cd /home/servuo

echo "Pulling latest from git..."
git checkout Config/DataPath.cfg 2>/dev/null || true
git pull

echo "Building Server.dll..."
dotnet build Server/Server.csproj -c Release --nologo -v minimal
cp Server/bin/Release/Server.dll /home/servuo-test/Server.dll
echo "Server.dll built."

echo "Building Scripts.dll..."
dotnet build Scripts/Scripts.csproj -c Release --nologo -v minimal
cp Scripts/bin/Release/Scripts.dll /home/servuo-test/Scripts.dll
echo "Scripts.dll built."

chmod +x /home/servuo-test/restart.sh 2>/dev/null || true

echo "Saving world before restart..."
docker exec servuo-test screen -S servuo -X stuff "worldsave$(printf '\r')" 2>/dev/null || true
sleep 15

echo "Fixing DataPath for Linux..."
sed -i 's|CustomPath=.*|CustomPath=/home/servuo/uodata|' /home/servuo/Config/DataPath.cfg
sed -i 's|CustomPath=.*|CustomPath=/home/servuo/uodata|' /home/servuo-test/Config/DataPath.cfg 2>/dev/null || true

echo "Setting test server port to 2594..."
sed -i 's|@\?Port=.*|Port=2594|' /home/servuo-test/Config/Server.cfg

echo "Killing existing test server..."
docker exec servuo-test bash -c "pkill -9 -f mono 2>/dev/null; sleep 2" 2>/dev/null || true

echo "Starting TEST server (port 2594)..."
docker exec -d servuo-test bash -c "cd /home/servuo && mono ServUO.exe -noconsole < /dev/null >> /home/servuo/servuo.log 2>&1"

echo "Deploy-test complete. Production server untouched."

echo "Deploy-test complete. Production server untouched."
