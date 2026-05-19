#!/bin/bash
# ============================================================
# start.sh
# ServUO startup script
#
# - Deletes cached Scripts.dll before launch to prevent
#   corrupt assembly crashes on Mono/Linux
# - Strips ANSI color codes from log so grep works cleanly
# - Runs server inside a named screen session called "servuo"
# ============================================================

cd /home/servuo

# Delete cached script assembly — forces clean recompile on startup
rm -f Scripts.dll Scripts.pdb

# Start server in a detached screen session
screen -dmS servuo bash -c \
  "mono ServUO.exe -noconsole < /dev/null 2>&1 | sed 's/\x1b\[[0-9;]*m//g' | tee -a /home/servuo/servuo.log"

echo "ServUO started in screen session 'servuo'"
echo "Attach with: screen -r servuo"
