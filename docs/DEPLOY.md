# Deploying RuleFlow behind a domain (HTTPS)

RuleFlow listens on **`http://0.0.0.0:8080`** by default (override with the `ASPNETCORE_URLS` env var).
For a public domain with HTTPS, put a reverse proxy (nginx or IIS) in front of it. Pick the section for your setup.

> First run: after it's reachable, open the site and complete the **installation wizard** (database, admin, AI).

---

## Nginx — Linux

1. Run RuleFlow (Docker, or the Linux installer as a service — both listen on `:8080`).
2. Create `/etc/nginx/sites-available/ruleflow.conf` (adjust `server_name`):

```nginx
server {
    listen 80;
    server_name ruleflow.example.com;

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   Upgrade           $http_upgrade;      # WebSockets (Blazor/SignalR)
        proxy_set_header   Connection        "upgrade";
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400s;                               # keep the circuit alive
    }
}
```

3. Enable it and reload:
```bash
sudo ln -s /etc/nginx/sites-available/ruleflow.conf /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

4. HTTPS with Let's Encrypt (auto-renews via the certbot systemd timer):
```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d ruleflow.example.com
sudo certbot renew --dry-run     # verify auto-renewal
```

---

## Nginx — Windows

1. Install RuleFlow as a service so it always runs on `:8080`:
   ```powershell
   $env:RULEFLOW_SERVICE=1; irm https://raw.githubusercontent.com/RoberthDudiver/ruleflow/main/install.ps1 | iex
   ```
2. Install [nginx for Windows](https://nginx.org/en/download.html) and use the **same `server` block** as the Linux
   example above (proxy to `http://127.0.0.1:8080`).
3. HTTPS on Windows: use **[win-acme](https://www.win-acme.com/)** (`wacs.exe`) to get and **auto-renew** a Let's
   Encrypt certificate, then reference the `.pem`/`.pfx` in the nginx `ssl_certificate` directives. (win-acme
   installs a scheduled task that renews automatically.)

---

## IIS — Windows

1. Install the **[.NET ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)** (adds the
   ASP.NET Core Module to IIS), then restart IIS: `net stop was /y && net start w3svc`.
2. Unzip the **win-x64** release into your site folder, e.g. `C:\inetpub\ruleflow`. The included **`web.config`**
   makes IIS run the app via the ASP.NET Core Module — no port needed (IIS hosts it in-process).
3. In **IIS Manager**:
   - Add a website → point it at `C:\inetpub\ruleflow`.
   - Set its **Application Pool** to **“No Managed Code”**.
   - Give the app-pool identity (e.g. `IIS AppPool\<pool>`) **write** permission on the folder (for `App_Data`
     and `pluginstore`).
4. HTTPS: in **Bindings** add an `https` binding and select a certificate. For free auto-renewing certificates use
   **win-acme**, which can bind the cert to the IIS site and renew it on a schedule.
5. Browse the site and complete the installation wizard.

> IIS hosts the app for you — you do **not** set `ASPNETCORE_URLS` and there is no separate `:8080` process.

---

## Notes

- **Data**: keep `App_Data` (SQLite + config) and `pluginstore` (uploaded plugin versions) on persistent storage
  — Docker volumes, or a backed-up folder for the binaries/IIS install.
- **Skip the wizard** (configure by environment instead): set `Setup__Skip=true` and the `Persistence__*` variables.
