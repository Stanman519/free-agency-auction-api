#!/usr/bin/env bash
# Run once on a fresh Ubuntu 24.04 ARM64 Oracle VM.
# Assumes you SSH'd in as the default 'ubuntu' user.

set -euo pipefail

### 1. Open firewall (Oracle Ubuntu image ships with restrictive iptables)
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save

### 2. Install Docker + nginx
sudo apt-get update
sudo apt-get install -y \
    ca-certificates curl gnupg \
    docker.io docker-compose-v2 \
    nginx
sudo usermod -aG docker "$USER"
sudo systemctl enable --now docker nginx

### 3. Layout
sudo mkdir -p /var/www/fantasy-league /etc/fantasy-league
sudo chown "$USER:$USER" /var/www/fantasy-league
sudo chmod 700 /etc/fantasy-league

cat <<'NOTE'

================================================================
Bootstrap done. Manual steps remaining:

1. SCP these files from your dev box into place:
   deploy/docker-compose.yml         → /var/www/fantasy-league/docker-compose.yml
   deploy/nginx/fantasy-league.conf  → /etc/nginx/sites-available/fantasy-league
   deploy/env-templates/*.env.example → /etc/fantasy-league/*.env  (then EDIT in real values, chmod 600)

2. Cloudflare Origin Certificate:
   Dashboard → fanpools.net → SSL/TLS → Origin Server → Create Certificate
   Hostnames: auction-api.fanpools.net, bot-api.fanpools.net  (15 yr)
   Save cert  → /etc/fantasy-league/origin.crt  (chmod 644)
   Save key   → /etc/fantasy-league/origin.key  (chmod 600)

3. Edit docker-compose.yml: replace REPLACE_WITH_GH_USER with your GH username.

4. Enable nginx site:
   sudo ln -s /etc/nginx/sites-available/fantasy-league /etc/nginx/sites-enabled/
   sudo rm -f /etc/nginx/sites-enabled/default
   sudo nginx -t && sudo systemctl reload nginx

5. Login to GHCR (one-time, so first compose pull works before any GH Action runs):
   echo <PAT_with_read:packages> | docker login ghcr.io -u <gh-user> --password-stdin
   (or push deploy via GH Actions first; the workflow auths during deploy)

6. Bring up services:
   cd /var/www/fantasy-league
   docker compose pull
   docker compose up -d

7. Smoke test:
   curl -I https://auction-api.fanpools.net/free-agency/test-data
   curl    https://bot-api.fanpools.net/Bot/pendingTrades

8. GitHub repo secrets (both repos):
   ORACLE_HOST    = <your reserved public IP>
   ORACLE_USER    = ubuntu
   ORACLE_SSH_KEY = <full private key contents incl. -----BEGIN ... ----->

9. Cloudflare DNS (orange cloud / proxied):
   A  auction-api  → <public IP>
   A  bot-api      → <public IP>
   SSL/TLS mode: Full (strict)

10. Update everything that points at the old Render URLs:
    - react-fa-auction CF Pages env: REACT_APP_AUCTION_API_URL, REACT_APP_BOT_API_URL
    - cap-chron-caller GH secrets: AUCTION_API_URL, BOT_API_URL
    - Auth0: Allowed Callback / Logout / Web Origin URLs
    - GroupMe bot callback URL: https://bot-api.fanpools.net/bot/contractSearch/{fakeYear}
================================================================

NOTE
