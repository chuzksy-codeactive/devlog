# Azure OIDC + GitHub Actions Setup Guide

Step-by-step runbook for wiring GitHub Actions to deploy to Azure (App Service + Static Web Apps) using OIDC federated credentials — no long-lived Azure secret ever stored in GitHub. Written as we did it for DevLog, updated as each step completes, so this doubles as a reference for setting the same thing up on a future project.

> **Update (cost-capped relaunch):** the active deploy flow no longer uses a staging slot swap workflow. `deploy.yml` now deploys the API directly to `app-devlog-api-prod-ca3c`, smoke-tests `/health`, then deploys frontend. Keep this file as historical setup context and prefer current workflow files for live behavior.

Companion to `DevLog-Deployment-Checklist.md` Phase 4 — this file is the detailed how-to; the checklist just has the summary checkbox.

---

## What this achieves

- `.github/workflows/deploy.yml` authenticates to Azure with a short-lived token requested at run time, not a stored credential.
- The deploy workflow authenticates from the `production` GitHub environment and deploys directly to the production API app.
- There is no slot swap workflow in the current cost-capped pipeline; `deploy.yml` smoke-tests production `/health` after API deploy and before frontend deploy.

---

## Values used for DevLog (fill in as you go)

| Value | Where it's used | Value |
|---|---|---|
| GitHub org/owner | Federated credential subject | `chuzksy-codeactive` |
| GitHub repo | Federated credential subject | `devlog` |
| App Registration name | Azure | `devlog-github-actions` |
| Application (client) ID | GitHub secret `AZURE_CLIENT_ID` | `d73b83db-839e-49b8-b4c6-ce2e05504af6` |
| Directory (tenant) ID | GitHub secret `AZURE_TENANT_ID` | `832a01ed-2dc4-4fde-99e4-b24a7b19d396` |
| Subscription ID | GitHub secret `AZURE_SUBSCRIPTION_ID` | `3c6c67ce-2bb9-4858-90f2-137a1e6ebe3c` |
| Resource group | GitHub variable `AZURE_RESOURCE_GROUP` | `rg-devlog-prod-ca3c` |
| App Service name | Hardcoded in `deploy.yml` | `app-devlog-api-prod-ca3c` |
| Static Web App name | Azure resource | `swa-devlog-prod-ca3c` |

---

## Step 1 — Create the Azure App Registration ✅ done

1. Azure Portal → search **"App registrations"** → **+ New registration**.
2. Name: `devlog-github-actions` (label only, doesn't affect function).
3. Supported account types: **"Single tenant only - Default Directory"** — this app only ever needs to authenticate against our own tenant, not other Microsoft tenants or personal accounts.
4. Redirect URI: leave blank.
5. **Register**.
6. From the Overview page, note down: **Application (client) ID** and **Directory (tenant) ID** (both in the table above).

## Step 2 — Add federated credentials ✅ done

Create one federated credential for the `production` GitHub environment used by `deploy.yml`.

1. App Registration → left sidebar → **Certificates & secrets** → tab **Federated credentials** → **+ Add credential**.
2. Scenario: **"GitHub Actions deploying Azure resources"**.
3. Fill in:
   - Organization: `chuzksy-codeactive`
   - Repository: `devlog`
   - Entity type: **Environment**
   - GitHub environment name: `production`
4. **Gotcha**: Azure's newer credential form also requires GitHub's **numeric** Organization ID and Repository ID, not just the names (an "immutable subject identifier" hardening measure). To find them:
   - Go to `https://github.com/<org>/<repo>` in a browser where you're already logged in.
   - Right-click → **View Page Source** (Ctrl+U).
   - Ctrl+F → search `repository_id` → the number in `<meta name="octolytics-dimension-repository_id" content="...">` is the **Repository ID**.
   - Ctrl+F → search `user_id` → the number in `<meta name="octolytics-dimension-user_id" content="...">` is the **Organization ID** (works the same whether the owner is a proper GitHub org or a personal account).
   - This only works via a real logged-in browser tab for a private repo — GitHub's public API 404s an unauthenticated request to a private repo.
5. Name: `github-production` → **Add**.

Result: **Federated credentials (1)** — `github-production` with a `Subject identifier` like `repo:chuzksy-codeactive@<org-id>/devlog@<repo-id>:environment:production`.

## Step 3 — Role assignment ✅ done

The federated credential only lets the app *authenticate*; it still needs a role granting it permission to actually deploy.

1. Azure Portal → search **"Subscriptions"** → copy the **Subscription ID** (table above).
2. Go to the `devlog-api` App Service.
3. Left sidebar → **Access control (IAM)** → **+ Add** → **Add role assignment**.
4. Role: **Website Contributor** (scoped to App Service management — deploy code, manage app settings, swap slots — without broader subscription-level Contributor access).
5. "Assign access to" → **User, group, or service principal** → **+ Select members** → search `devlog-github-actions` → select it → **Select**.
6. **Review + assign** ×2 to confirm.

---

## Step 4 — GitHub repo secrets and variables ✅ done

Also created a dedicated Clerk **Production** instance along the way (previously only the Development instance keys were in use) — "Primary application" for chuzksy.dev, so Clerk's API lives at `clerk.chuzksy.dev` and auth emails send from `@chuzksy.dev`. Five CNAME records added at Porkbun for that (Frontend API, Account portal, Primary email + 2 DKIM selectors), all verified.

Repo → Settings → Secrets and variables → Actions.

**Secrets** (Secrets tab → New repository secret) — all 6 added:
- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` — from the table above.
- `CLERK_SECRET_KEY` — the production Clerk secret key value from Clerk's Production instance API Keys page.
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` — the production Clerk publishable key. Public by nature, but stored in Secrets for this repo's deploy flow to avoid empty-value variable resolution during SWA deploy.
- `AZURE_STATIC_WEB_APPS_API_TOKEN` — from the Static Web App resource: Overview → **Manage deployment token**. Scoped to just that one Static Web App — can't touch anything else in Azure, unlike the OIDC credential.

**Variables** (Variables tab → New repository variable):
- `AZURE_RESOURCE_GROUP` — confirmed as `coursera_ai_learning`.

## Step 5 — Create the GitHub environment ✅ done

Repo → Settings → **Environments**.

- `production` — created for OIDC trust and deployment audit history.

## Step 6 — Verify — next

- Push a commit to `main` (or merge a PR) and watch the **Actions** tab.
- `build-api` → `deploy-api-production` → `smoke-test-api` → `deploy-frontend` should run automatically.
- Confirm the API `/health` endpoint returns `200` and the frontend reflects the new deploy.
