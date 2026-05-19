#!/bin/bash
# deploy.sh — pull latest from git, update website, restart ServUO
set -e

cd /home/servuo

echo "Pulling latest from git..."
git pull

echo "Deploying website..."
cp website/index.html /var/www/html/index.html
cp website/submit.php /var/www/html/submit.php
cp website/admin.php /var/www/html/admin.php
cp website/update-status.php /var/www/html/update-status.php

chmod +x /home/servuo/restart.sh 2>/dev/null || true

echo "Restarting ServUO..."
/home/servuo/restart.sh

echo "Deploy complete."
