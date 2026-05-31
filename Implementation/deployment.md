# Deployment Guide — Fun Stuff UO Shard

This document describes how code changes are developed, committed, and deployed to the test and production ServUO servers.

---

## Infrastructure overview

| Thing | Detail |
|---|---|
| VPS host | Ubuntu VPS at `178.105.173.80` |
| Prod container | `servuo` — game port 2593, shard name "Fun Stuff" |
| Test container | `servuo-test` — game port 2594, shard name "Fun Stuff test" |
| Git repo (prod files) | `/home/servuo/` on the VPS |
| Test server files | `/home/servuo-test/` on the VPS |
| UO game data | `/home/servuo/uodata/` |
| Server timezone | UTC (Jacob is CEST = UTC+2) |

The git repository lives at `/home/servuo/`. The prod server runs directly out of that directory. The test server at `/home/servuo-test/` is updated by the deploy-test script, which copies changed files across.

---

## The golden rule

**Always deploy to test first. Never touch prod without a successful test deployment.**

---

## Normal workflow

### 1. Make changes (on PC)

Edit files in `C:\UO\SERVUO\Scripts\Custom\` (or wherever appropriate). ServUO auto-compiles all scripts in `Scripts/` on server restart — there is no manual build step.

### 2. Commit and push (on PC)

```
git add Scripts/Custom/MyFile.cs
git commit -m "Short description of what changed"
git push
```

### 3. Deploy to test (on server)

```bash
cd /home/servuo && git pull --no-edit && bash /home/servuo/deploy-test.sh
```

### 4. Wait for test server to come up (on server)

```bash
until docker exec servuo-test bash -c "cat /proc/net/tcp | grep -q '00000000:0A22'"; do echo "$(date +%H:%M:%S) — loading..."; sleep 5; done && echo "UP"
```

Port `0A22` = 2594 in hex = test server port.

### 5. Test in-game

Connect to port 2594. Test the change. If something is wrong, fix it, commit, and redeploy to test.

### 6. Deploy to prod (on server — only after test passes)

```bash
cd /home/servuo && bash /home/servuo/deploy.sh
```

### 7. Wait for prod to come up (on server)

```bash
until docker exec servuo bash -c "cat /proc/net/tcp | grep -q '00000000:0A21'"; do echo "$(date +%H:%M:%S) — loading..."; sleep 5; done && echo "UP"
```

Port `0A21` = 2593 in hex = prod server port.

---

## Checking server status

```bash
# Is prod up?
docker exec servuo bash -c "cat /proc/net/tcp | grep -q '00000000:0A21' && echo 'UP' || echo 'DOWN'"

# Is test up?
docker exec servuo-test bash -c "cat /proc/net/tcp | grep -q '00000000:0A22' && echo 'UP' || echo 'DOWN'"
```

Use `/proc/net/tcp` — `ss` and `netstat` are not available inside Docker containers on this setup.

---

## Checking logs

```bash
# Last 20 lines of prod server log
docker exec servuo bash -c "strings /home/servuo/servuo.log | tail -20"

# Watchdog log (monitors prod, auto-restarts if hung — runs every 5 min via cron)
cat /home/servuo/watchdog.log | tail -10
```

---

## Environment-specific configs

The `Config/env/prod/` and `Config/env/test/` directories contain environment-specific overrides (DataPath, Server.cfg, etc.). The deploy scripts apply these automatically. Never manually patch `Config/DataPath.cfg` or `Config/Server.cfg` on the server — it will break the next deploy.

---

## Important gotchas

**Screen is broken in the test container.** Never use `restart.sh` or `screen` for the test server. The deploy-test script handles everything via `docker exec -d`.

**Do not mix PC and server commands.** Always label them separately as "On PC:" and "On server:" when writing instructions to avoid confusion.

**Do not read server files with shell commands.** When reviewing code, use the Read tool on local files at `C:\UO\SERVUO\`. Do not SSH into the server to read files.

**Scripts compile on restart.** There is no manual build step. If a script has a compile error the server will still start but that script will be skipped. Check the server log after restart to catch compile errors.

---

## Quick-reference command card

```bash
# Full test deploy
cd /home/servuo && git pull --no-edit && bash /home/servuo/deploy-test.sh

# Full prod deploy (only after test passes)
cd /home/servuo && bash /home/servuo/deploy.sh

# Is test up?
docker exec servuo-test bash -c "cat /proc/net/tcp | grep -q '00000000:0A22' && echo 'UP' || echo 'DOWN'"

# Is prod up?
docker exec servuo bash -c "cat /proc/net/tcp | grep -q '00000000:0A21' && echo 'UP' || echo 'DOWN'"

# Wait for test
until docker exec servuo-test bash -c "cat /proc/net/tcp | grep -q '00000000:0A22'"; do echo "$(date +%H:%M:%S) — loading..."; sleep 5; done && echo "UP"

# Wait for prod
until docker exec servuo bash -c "cat /proc/net/tcp | grep -q '00000000:0A21'"; do echo "$(date +%H:%M:%S) — loading..."; sleep 5; done && echo "UP"

# Prod log
docker exec servuo bash -c "strings /home/servuo/servuo.log | tail -20"

# Watchdog log
cat /home/servuo/watchdog.log | tail -10
```
