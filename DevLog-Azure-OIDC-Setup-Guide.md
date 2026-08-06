# Azure OIDC + GitHub Actions Setup Guide

Step-by-step runbook for wiring GitHub Actions to deploy to Azure (App Service + Static Web Apps) using OIDC federated credentials — no long-lived Azure secret ever stored in GitHub. Written as we did it for DevLog, updated as each step completes, so this doubles as a reference for setting the same thing up on a future project.

Companion to `DevLog-Deployment-Checklist.md` Phase 4 — this file is the detailed how-to; the checklist just has the summary checkbox.

---

## What this achieves

- `.github/workflows/deploy.yml` authenticates to Azure with a short-lived token requested at run time, not a stored credential.
- Two GitHub **environments** (`staging`, `production`) each get their own trust relationship — a workflow run can only mint a token for the environment its job actually targets.
- The "manual approval before swap" gate turned out **not** to be GitHub's native required-reviewers rule — that's Enterprise-only for private repos. Instead, `swap-to-production.yml` is a separate `workflow_dispatch`-only workflow: nothing runs until someone manually clicks "Run workflow" and types `swap` to confirm. See Step 5.

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
| Resource group | GitHub variable `AZURE_RESOURCE_GROUP` | `coursera_ai_learning` |
| App Service name | Hardcoded in `deploy.yml` | `devlog-api` |
| Static Web App name | Hardcoded in `deploy.yml` | `devlog-web` |

---

## Step 1 — Create the Azure App Registration ✅ done

1. Azure Portal → search **"App registrations"** → **+ New registration**.
2. Name: `devlog-github-actions` (label only, doesn't affect function).
3. Supported account types: **"Single tenant only - Default Directory"** — this app only ever needs to authenticate against our own tenant, not other Microsoft tenants or personal accounts.
4. Redirect URI: leave blank.
5. **Register**.
6. From the Overview page, note down: **Application (client) ID** and **Directory (tenant) ID** (both in the table above).

## Step 2 — Add federated credentials ✅ done

One per GitHub **environment** a workflow job authenticates from — `deploy.yml` has two jobs that call `azure/login`, gated on two different environments (`staging`, `production`), so this needs two separate credentials.

1. App Registration → left sidebar → **Certificates & secrets** → tab **Federated credentials** → **+ Add credential**.
2. Scenario: **"GitHub Actions deploying Azure resources"**.
3. Fill in:
   - Organization: `chuzksy-codeactive`
   - Repository: `devlog`
   - Entity type: **Environment**
   - GitHub environment name: `staging`
4. **Gotcha**: Azure's newer credential form also requires GitHub's **numeric** Organization ID and Repository ID, not just the names (an "immutable subject identifier" hardening measure). To find them:
   - Go to `https://github.com/<org>/<repo>` in a browser where you're already logged in.
   - Right-click → **View Page Source** (Ctrl+U).
   - Ctrl+F → search `repository_id` → the number in `<meta name="octolytics-dimension-repository_id" content="...">` is the **Repository ID**.
   - Ctrl+F → search `user_id` → the number in `<meta name="octolytics-dimension-user_id" content="...">` is the **Organization ID** (works the same whether the owner is a proper GitHub org or a personal account).
   - This only works via a real logged-in browser tab for a private repo — GitHub's public API 404s an unauthenticated request to a private repo.
5. Name: `github-staging` → **Add**.
6. Repeat the whole flow again for the `production` environment, name it `github-production`.

Result: **Federated credentials (2)** — `github-staging` and `github-production` — each with a `Subject identifier` like `repo:chuzksy-codeactive@<org-id>/devlog@<repo-id>:environment:staging`.

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

Also created a dedicated Clerk **Production** instance along the way (was still only using the Development instance's `pk_test_`/`sk_test_` keys) — "Primary application" for chuzksy.dev, so Clerk's API lives at `clerk.chuzksy.dev` and auth emails send from `@chuzksy.dev`. Five CNAME records added at Porkbun for that (Frontend API, Account portal, Primary email + 2 DKIM selectors), all verified.

Repo → Settings → Secrets and variables → Actions.

**Secrets** (Secrets tab → New repository secret) — all 5 added:
- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` — from the table above.
- `CLERK_SECRET_KEY` — the real production value, `sk_live_...`, from Clerk's Production instance API Keys page (not the `sk_test_...` dev key).
- `AZURE_STATIC_WEB_APPS_API_TOKEN` — from the Static Web App resource: Overview → **Manage deployment token**. Scoped to just that one Static Web App — can't touch anything else in Azure, unlike the OIDC credential.

**Variables** (Variables tab → New repository variable) — both added:
- `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` — the `pk_live_...` production key. Not secret (ships in the client JS bundle either way).
- `AZURE_RESOURCE_GROUP` — confirmed as `coursera_ai_learning`.

## Step 5 — Create the GitHub environments ✅ done

Repo → Settings → **Environments**.

- `staging` — created. No required reviewers needed (it's just there for a distinct audit-trail entry, matching the federated credential from Step 2).
- `production` — created. **Gotcha**: the plan was to add a required reviewer here so GitHub would pause `swap-api-to-production` until a human approved it — but the "Deployment protection rules" / "Required reviewers" section doesn't appear on the "Configure production" page at all. Confirmed this is Enterprise-only for private repositories (Free/Pro/Team plans only get it on *public* repos) — same licensing wall hit earlier with Advanced Security / secret scanning.
- **Workaround, implemented**: the swap step moved out of `deploy.yml` entirely into its own file, `swap-to-production.yml`, triggered only by manually clicking "Run workflow" in the Actions tab (`workflow_dispatch`), with a required `confirm` text input (must type `swap` exactly) as a second speed bump. It still targets the `production` environment for OIDC trust and audit-trail purposes — it just doesn't get (or need) a required-reviewer pause on top, since nothing runs at all until someone deliberately triggers it. `DevLog-Deployment-Checklist.md` has the same note.

## Step 6 — Verify — next

- Push a commit to `main` (or merge a PR) and watch the **Actions** tab.
- `build-api` → `deploy-api-staging` → `smoke-test-staging` → `deploy-frontend` should all run automatically.
- Once that's green, go to **Actions → Swap staging → production → Run workflow**, type `swap` in the confirm box, and run it manually to confirm the gate actually works before trusting it for a real deploy.
- Confirm `api.chuzksy.dev` and `app.chuzksy.dev` both reflect the new deploy afterward.
