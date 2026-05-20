#!/bin/bash
# ============================================================
# start.sh
# ServUO startup script
#
# - Strips ANSI color codes from log so grep works cleanly
# - Runs server inside a named screen session called "servuo"
# ============================================================

cd /home/servuo

# Start server in a detached screen session
# Use direct file redirection to avoid pipe buffer deadlocks
screen -dmS servuo bash -c \
  "mono ServUO.exe -noconsole < /dev/null >> /home/servuo/servuo.log 2>&1"

echo "ServUO started in screen session 'servuo'"
echo "Attach with: screen -r servuo"
