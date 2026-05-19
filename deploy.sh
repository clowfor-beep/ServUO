#!/bin/bash
# deploy.sh — pull latest from git and update the website
set -e

cd /home/servuo

echo "Pulling latest from git..."
git pull

echo "Deploying website..."
cp website/index.html /var/www/html/index.html
cp website/submit.php /var/www/html/submit.php

echo "Done. Website is up to date."
