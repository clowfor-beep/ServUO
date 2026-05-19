#!/bin/bash
# deploy-test.sh — build Scripts.dll and restart the TEST server only
# Run this on the HOST (not inside the Docker container)
# Production server is NOT affected.
set -e

cd /home/servuo

echo "Pulling latest from git..."
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

echo "Restarting TEST server (port 2594)..."
docker exec servuo-test /home/servuo/restart.sh

echo "Deploy-test complete. Production server untouched."
