#!/usr/bin/env bash
# =============================================================
# apply-nginx-security.sh
# AIther -- nginx security hardening
#
# Run on the VPS as root:
#   bash apply-nginx-security.sh
# =============================================================
set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; NC='\033[0m'
ok()   { echo -e "${GREEN}[OK]${NC}   $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
err()  { echo -e "${RED}[ERR]${NC}  $1"; }
info() { echo -e "${CYAN}[..]${NC}   $1"; }

echo ""
echo "  AIther -- nginx security hardening"
echo "  -----------------------------------------"
echo ""

if [ "$EUID" -ne 0 ]; then
    err "Must run as root: sudo bash $0"
    exit 1
fi

# -- Step 1: Rate-limit zone (http context, auto-included via conf.d) ----------
RATELIMIT_FILE="/etc/nginx/conf.d/aither-ratelimit.conf"

if grep -rq "submit_limit" /etc/nginx/ 2>/dev/null; then
    warn "Rate limit zone already present -- skipping."
else
    cat > "$RATELIMIT_FILE" << 'ENDCONF'
# AIther -- submission rate limit zone
# 5 POST requests per minute per IP address
limit_req_zone $binary_remote_addr zone=submit_limit:10m rate=5r/m;
ENDCONF
    ok "Rate limit zone created: $RATELIMIT_FILE"
fi

# -- Step 2: Find the nginx site config ----------------------------------------
info "Locating nginx site config..."

SITE_CONF=$(grep -rl "servuo/website" /etc/nginx/ 2>/dev/null \
    | grep -v "\.bak" | head -1 || true)

if [ -z "$SITE_CONF" ]; then
    for candidate in \
        /etc/nginx/sites-enabled/default \
        /etc/nginx/sites-enabled/funstuffuo \
        /etc/nginx/conf.d/default.conf; do
        if [ -f "$candidate" ]; then SITE_CONF="$candidate"; break; fi
    done
fi

if [ -z "$SITE_CONF" ]; then
    err "Cannot find the nginx site config automatically."
    echo ""
    echo "  Find it yourself with:  grep -rl 'servuo' /etc/nginx/"
    echo "  Then re-run:            SITE_CONF=/path/to/config bash $0"
    echo ""
    exit 1
fi

ok "Found site config: $SITE_CONF"

# -- Step 3: Idempotency check -------------------------------------------------
if grep -q "AITHER_SECURITY" "$SITE_CONF"; then
    warn "Security config already applied -- nothing to patch."
    info "Testing and reloading nginx..."
    nginx -t 2>&1 && nginx -s reload
    ok "nginx reloaded."
    exit 0
fi

# -- Step 4: Backup ------------------------------------------------------------
# Store backup in /etc/nginx/ not alongside the config — if the config
# lives in sites-enabled/, a .bak there would be loaded by nginx as a
# second server block and cause a duplicate default_server conflict.
BACKUP="/etc/nginx/$(basename "$SITE_CONF").bak.$(date +%Y%m%d_%H%M%S)"
cp "$SITE_CONF" "$BACKUP"
ok "Backed up to: $BACKUP"

# -- Step 5: Detect PHP-FPM socket --------------------------------------------
PHP_SOCK=$(grep -oP "unix:\K[^;]+" "$SITE_CONF" 2>/dev/null \
    | grep "fpm.sock" | head -1 || true)

if [ -z "$PHP_SOCK" ]; then
    PHP_SOCK=$(find /run/php/ -name "*fpm.sock" 2>/dev/null | head -1 || true)
fi

if [ -z "$PHP_SOCK" ]; then
    warn "Could not detect PHP-FPM socket. Using php8.1 default."
    PHP_SOCK="/run/php/php8.1-fpm.sock"
fi

ok "PHP-FPM socket: $PHP_SOCK"

# -- Step 6: Inject security block via Python ----------------------------------
info "Patching $SITE_CONF..."

python3 - "$SITE_CONF" "$PHP_SOCK" << 'PYEOF'
import sys, re

conf_path = sys.argv[1]
php_sock  = sys.argv[2]

with open(conf_path, 'r') as f:
    content = f.read()

inject = (
    "\n"
    "    # ============================================================\n"
    "    # AITHER_SECURITY -- applied by apply-nginx-security.sh\n"
    "    # ============================================================\n"
    "\n"
    "    server_tokens off;\n"
    "\n"
    '    add_header X-Content-Type-Options  "nosniff"       always;\n'
    '    add_header X-Frame-Options         "DENY"          always;\n'
    '    add_header Referrer-Policy         "same-origin"   always;\n'
    '    add_header X-XSS-Protection        "1; mode=block" always;\n'
    "\n"
    "    # Rate-limit /submit.php: 5 req/min per IP, burst of 2.\n"
    "    # Exact-match (=) takes priority over catch-all PHP blocks.\n"
    "    location = /submit.php {\n"
    "        limit_req        zone=submit_limit burst=2 nodelay;\n"
    "        limit_req_status 429;\n"
    f"        fastcgi_pass     unix:{php_sock};\n"
    "        include          fastcgi_params;\n"
    "        fastcgi_param    SCRIPT_FILENAME $document_root$fastcgi_script_name;\n"
    "    }\n"
    "\n"
    "    # Block installer/build artefacts from being downloaded\n"
    "    location ~* \\.(iss|bat|ps1)$ {\n"
    "        deny   all;\n"
    "        return 404;\n"
    "    }\n"
    "    location = /sqlite3.exe {\n"
    "        deny   all;\n"
    "        return 404;\n"
    "    }\n"
    "\n"
    "    # No-cache on live player count\n"
    "    location = /playercount.json {\n"
    '        add_header Cache-Control "no-cache, no-store" always;\n'
    "    }\n"
    "\n"
    "    # ============================================================\n"
    "    # END AITHER_SECURITY\n"
    "    # ============================================================\n"
    "\n"
)

found = [False]
def do_inject(m):
    found[0] = True
    return m.group(0) + inject

patched = re.sub(r'server\s*\{', do_inject, content, count=1)

if not found[0]:
    print("ERROR: could not find 'server {' block.", file=sys.stderr)
    sys.exit(1)

with open(conf_path, 'w') as f:
    f.write(patched)

print("Patched OK.")
PYEOF

if [ $? -ne 0 ]; then
    err "Patching failed. Restoring backup."
    cp "$BACKUP" "$SITE_CONF"
    exit 1
fi

ok "Site config patched."

# -- Step 7: Test and reload ---------------------------------------------------
echo ""
info "Testing nginx configuration..."
echo ""

if nginx -t 2>&1; then
    echo ""
    nginx -s reload
    ok "nginx reloaded successfully."
else
    echo ""
    err "nginx config test FAILED. Restoring backup."
    cp "$BACKUP" "$SITE_CONF"
    warn "Original restored from: $BACKUP"
    info "Backup kept at: $BACKUP"
    exit 1
fi

echo ""
echo "  -----------------------------------------"
ok "Security hardening complete. Applied:"
echo ""
echo "  - Rate limit on /submit.php: 5 requests/min per IP"
echo "  - Security headers: X-Content-Type-Options, X-Frame-Options,"
echo "                      Referrer-Policy, X-XSS-Protection"
echo "  - nginx version hidden from response headers"
echo "  - Blocked downloads: .iss  .bat  .ps1  sqlite3.exe"
echo "  - No-cache on /playercount.json"
echo ""
echo "  Backup saved at: $BACKUP"
echo ""
warn "Once you have HTTPS + a domain, also add:"
echo "    add_header Strict-Transport-Security \"max-age=31536000\" always;"
echo "  and set 'secure' => true in admin.php session_set_cookie_params."
echo ""
