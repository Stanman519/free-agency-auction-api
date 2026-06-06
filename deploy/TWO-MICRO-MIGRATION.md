# Migrating from one paid E2.2 VM → two free E2.1.Micro VMs

**Goal:** move `auction-api` and `bot-api` off the paid `VM.Standard.E2.2` (trial expired)
onto two Always-Free `VM.Standard.E2.1.Micro` instances — one API each. Zero cost, no app changes.

**Why two boxes:** each Micro has only 1 GB RAM. One API per box (~220 MB each measured) leaves
plenty of headroom so a busy auction night on `auction-api` can't slow down the bot, and vice versa.

**What does NOT change:** the React frontend, Auth0, GroupMe callback, and cap-chron-caller all point
at the hostnames `auction-api.fanpools.net` / `bot-api.fanpools.net`. We only repoint DNS, so none of
those need touching. DB stays on Neon (nothing on the VM is lost).

---

## Layout after migration

| | Micro #1 (auction) | Micro #2 (bot) |
|---|---|---|
| OCI name | `fanpools-auction` | `fanpools-bot` |
| Runs | `auction-api` container | `bot-api` container |
| Compose file (host) | `docker-compose.auction.yml` → save AS `docker-compose.yml` | `docker-compose.bot.yml` → save AS `docker-compose.yml` |
| nginx conf | `nginx/auction-api.conf` | `nginx/bot-api.conf` |
| Env file (host) | `/etc/fantasy-league/auction-api.env` | `/etc/fantasy-league/bot-api.env` |
| DNS A record | `auction-api` → Micro #1 IP | `bot-api` → Micro #2 IP |
| Repo whose ORACLE_HOST = this IP | `free-agency-auction-api` | `dead-cap-tracker` |

---

## Step 1 — Provision the two Micros

**Option A (recommended, no Oracle UI):** GitHub → `cap-chron-caller` repo → **Actions** tab →
**"Provision two E2.1.Micro (Always Free) instances"** → **Run workflow**. When it finishes,
open the run log — the last lines print the two public IPs. Write them down.

**Option B (Oracle Console clicks):** for each of the two instances:
1. cloud.oracle.com → ☰ menu → **Compute → Instances** → **Create instance**
2. **Name:** `fanpools-auction` (then `fanpools-bot` the second time)
3. **Image and shape** → **Edit shape** → **Specialty and previous generation** → pick
   **VM.Standard.E2.1.Micro** (it shows an *"Always Free-eligible"* green tag). Image: Canonical Ubuntu 22.04.
4. **Networking:** choose the **same VCN/subnet** as your current VM; **Assign a public IPv4 address = Yes**.
5. **Add SSH keys:** paste the same public key you used before (or upload a new one — must match the
   private key in the repos' `ORACLE_SSH_KEY` secret).
6. **Create.** When it's **Running**, copy its **Public IP** from the instance details page.

---

## Step 2 — Bootstrap each Micro (run on BOTH)

SSH in: `ssh ubuntu@<micro-ip>`  (use the key matching what you provisioned with)

Copy `deploy/bootstrap-vm.sh` up and run it — it adds the 2 GB swapfile, opens the firewall,
and installs Docker + nginx:
```bash
scp deploy/bootstrap-vm.sh ubuntu@<micro-ip>:~
ssh ubuntu@<micro-ip> 'bash ~/bootstrap-vm.sh'
```

---

## Step 3 — Copy config onto each Micro

**Auction Micro #1:**
```bash
scp deploy/docker-compose.auction.yml          ubuntu@<auction-ip>:/var/www/fantasy-league/docker-compose.yml
scp deploy/nginx/auction-api.conf              ubuntu@<auction-ip>:/tmp/fantasy-league.conf
scp deploy/env-templates/auction-api.env.example ubuntu@<auction-ip>:/tmp/auction-api.env
```
**Bot Micro #2:**
```bash
scp deploy/docker-compose.bot.yml              ubuntu@<bot-ip>:/var/www/fantasy-league/docker-compose.yml
scp deploy/nginx/bot-api.conf                  ubuntu@<bot-ip>:/tmp/fantasy-league.conf
scp deploy/env-templates/bot-api.env.example   ubuntu@<bot-ip>:/tmp/bot-api.env
```

Then on **each** Micro:
```bash
# nginx
sudo mv /tmp/fantasy-league.conf /etc/nginx/sites-available/fantasy-league
sudo ln -sf /etc/nginx/sites-available/fantasy-league /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default

# env file (FILL IN real secrets first!) — auction-api.env on #1, bot-api.env on #2
sudo mv /tmp/auction-api.env /etc/fantasy-league/auction-api.env   # (bot: bot-api.env)
# IMPORTANT: docker compose + the GH deploy run as the `ubuntu` user, so it must own this dir
sudo chown -R ubuntu:ubuntu /etc/fantasy-league
sudo chmod 700 /etc/fantasy-league
sudo chmod 600 /etc/fantasy-league/*.env

# edit docker-compose.yml: replace REPLACE_WITH_GH_USER with your GitHub username
sudo nano /var/www/fantasy-league/docker-compose.yml
```
Fill the env file with the **same real values** you already use on the E2.2. Easiest way to grab them:
on the old VM run `cat /etc/fantasy-league/auction-api.env` (and `bot-api.env`) and copy across.

---

## Step 4 — Cloudflare Origin certificate (on each Micro)

The existing origin cert already lists *both* hostnames, so you can reuse the **same** cert+key on both boxes.
- If you still have `origin.crt` / `origin.key`: scp them to `/etc/fantasy-league/` on each Micro.
- If not: Cloudflare dashboard → **fanpools.net → SSL/TLS → Origin Server → Create Certificate**
  (hostnames `auction-api.fanpools.net`, `bot-api.fanpools.net`, 15 yr). Save cert → `origin.crt`, key → `origin.key`.

On each Micro:
```bash
sudo chmod 644 /etc/fantasy-league/origin.crt
sudo chmod 600 /etc/fantasy-league/origin.key
sudo nginx -t && sudo systemctl reload nginx
```

---

## Step 5 — Bring up the containers (each Micro)

```bash
# one-time GHCR login so the first pull works (read:packages PAT)
echo <PAT> | docker login ghcr.io -u <gh-user> --password-stdin
cd /var/www/fantasy-league
docker compose pull
docker compose up -d
docker compose logs -f   # ctrl-C once you see it listening on :8080
free -h                  # sanity: used well under 1 GB
```

---

## Step 6 — Point GitHub deploys at the new hosts

No workflow edits needed — just update the per-repo secret:
- `free-agency-auction-api` repo → Settings → Secrets → **`ORACLE_HOST`** = **auction Micro IP**
- `dead-cap-tracker` repo → Settings → Secrets → **`ORACLE_HOST`** = **bot Micro IP**

(Leave `ORACLE_USER=ubuntu` and `ORACLE_SSH_KEY` as-is, assuming you reused the same key.)

---

## Step 7 — Flip DNS (Cloudflare) — this is the cutover

Cloudflare dashboard → **fanpools.net → DNS → Records**:
- Edit the **`auction-api`** A record → content = **auction Micro IP** (keep proxy **ON / orange cloud**)
- Edit the **`bot-api`** A record → content = **bot Micro IP** (proxy ON / orange)

Propagates in seconds (proxied). Now test:
```bash
curl -I https://auction-api.fanpools.net/free-agency/test-data
curl    https://bot-api.fanpools.net/Bot/pendingTrades
```
Load the site (fanpools.net), place a test bid, confirm the GroupMe bot still answers.

---

## Step 8 — Decommission the paid E2.2

Once both Micros serve traffic and the site looks healthy for a day:
Oracle Console → **Compute → Instances → `fanpools-vm`** → **More actions → Terminate**
(check "delete attached boot volume"). This is what stops Oracle from eventually reclaiming it on its own
schedule and removes the "data loss" warning. Nothing important lived on it (DB is on Neon).

---

## Rollback (if a Micro misbehaves)

DNS is the switch. Re-point the affected A record back to the old E2.2 IP in Cloudflare → traffic returns
to the old box within seconds. So **don't terminate the E2.2 until you're confident** (Step 8 last).

## Frontend / external — confirm nothing to do

- React app (Cloudflare Pages): targets the hostnames → **no change**.
- Auth0 callback/logout/origins: point at `fanpools.net` (frontend) → **no change**.
- GroupMe bot callback `https://bot-api.fanpools.net/...` → **no change** (DNS repointed).
- cap-chron-caller secrets `AUCTION_API_URL` / `BOT_API_URL` (hostnames) → **no change**.
