#!/bin/bash
# deploy.sh — pull latest, build Scripts.dll, update website, restart ServUO
# Run this on the HOST (not inside the Docker container)
set -e

cd /home/servuo

echo "Pulling latest from git..."
git pull

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
docker exec servuo screen -S servuo -X stuff "worldsave$(printf '\r')" 2>/dev/null || true
sleep 15

echo "Restarting ServUO..."
docker exec servuo /home/servuo/restart.sh

echo "Deploy complete."
