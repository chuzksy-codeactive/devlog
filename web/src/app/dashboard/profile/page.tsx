"use client";

import { useEffect, useRef, useState } from "react";
import { useReverification, useUser } from "@clerk/nextjs";
import { isReverificationCancelledError } from "@clerk/nextjs/errors";
import { useApi } from "@/lib/use-api";
import { Discipline, MyPortfolio, PortfolioVisibility, ProofLink } from "@/lib/types";
import { disciplineDisplayName, disciplineLabels } from "@/lib/discipline-labels";

type FormState = {
  displayName: string;
  headline: string;
  positioningLine: string;
  photoUrl: string;
  currentRole: string;
  yearsExperience: string;
  coreTechnologies: string; // comma-separated in the form, array on the wire
  linkedInUrl: string;
  gitHubUrl: string;
  gitHubVerified: boolean;
  cvFileUrl: string;
  discipline: Discipline;
  proofLinks: ProofLink[];
  visibility: PortfolioVisibility;
  discoverable: boolean;
  notificationEmail: string;
};

function toFormState(p: MyPortfolio): FormState {
  return {
    displayName: p.displayName,
    headline: p.headline,
    positioningLine: p.positioningLine,
    photoUrl: p.photoUrl ?? "",
    currentRole: p.currentRole ?? "",
    yearsExperience: p.yearsExperience ?? "",
    coreTechnologies: p.coreTechnologies.join(", "),
    linkedInUrl: p.linkedInUrl ?? "",
    gitHubUrl: p.gitHubUrl ?? "",
    gitHubVerified: p.gitHubVerified,
    cvFileUrl: p.cvFileUrl ?? "",
    discipline: p.discipline,
    proofLinks: p.proofLinks,
    visibility: p.visibility,
    discoverable: p.discoverable,
    notificationEmail: p.notificationEmail ?? "",
  };
}

export default function ProfilePage() {
  const { request } = useApi();
  const { user } = useUser();
  const [slug, setSlug] = useState<string | null>(null);
  const [form, setForm] = useState<FormState | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "saving" | "saved" | "error">("loading");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [gitHubStatus, setGitHubStatus] = useState<"idle" | "connecting" | "syncing">("idle");
  const [gitHubErrorMessage, setGitHubErrorMessage] = useState<string | null>(null);
  // Guards the auto-sync effect below from firing more than once per page
  // load — without it, a sync that comes back not-yet-verified (Clerk's
  // OAuth callback can take a moment to fully record) would otherwise retry
  // on every render instead of just once after the redirect back.
  const autoSyncedRef = useRef(false);

  // Clerk treats linking a new external account as a "sensitive" operation
  // that requires session reverification (a fresh password/MFA check),
  // independent of and unrelated to anything on DevLog's own backend or the
  // Clerk:SecretKey config — the very first request this flow makes
  // (POST .../v1/me/external_accounts) is browser-to-Clerk directly, before
  // DevLog's own sync endpoint is ever called. Without this wrapper,
  // createExternalAccount just throws a 403 "session_reverification_required"
  // outright; wrapping it in useReverification is what makes Clerk show its
  // own step-up-auth modal and automatically retry once the user clears it.
  const createExternalAccountWithReverification = useReverification(
    (params: { strategy: "oauth_github"; redirectUrl: string }) => user!.createExternalAccount(params)
  );

  useEffect(() => {
    request<MyPortfolio>("/api/portfolio/me")
      .then((data) => {
        setSlug(data.slug);
        setForm(toFormState(data));
        setStatus("ready");
      })
      .catch(() => setStatus("error"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Milestone 19 — after Clerk's own OAuth flow redirects back here,
  // user.externalAccounts (Clerk's client-side view) shows the new
  // connection before DevLog's own backend knows about it. This doesn't
  // trust that client-side view for anything — it's only a trigger to ask
  // the backend to independently confirm via Clerk's Backend API (see
  // ClerkUserService) and persist the result.
  useEffect(() => {
    if (autoSyncedRef.current || !form || form.gitHubVerified || !user) {
      return;
    }
    // "github" here, not "oauth_github" — Clerk's frontend SDK reports the
    // bare provider name on ExternalAccountResource.provider, while its
    // Backend API (what ClerkUserService calls server-side) reports the
    // full "oauth_github" strategy name for the same field. Two different
    // casings for the same fact, from the same vendor, depending which
    // side of the wire you're on — worth remembering if this check ever
    // silently stops matching after a Clerk SDK upgrade.
    const hasGitHubExternalAccount = user.externalAccounts?.some((a) => a.provider === "github");
    if (!hasGitHubExternalAccount) {
      return;
    }
    autoSyncedRef.current = true;
    syncGitHub();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form, user]);

  async function syncGitHub() {
    setGitHubStatus("syncing");
    try {
      const updated = await request<MyPortfolio>("/api/portfolio/me/github/sync", { method: "POST" });
      setForm((prev) => (prev ? { ...prev, gitHubUrl: updated.gitHubUrl ?? "", gitHubVerified: updated.gitHubVerified } : prev));
    } catch {
      // Best-effort — if this fails the field just stays in its previous
      // state and the "Connect GitHub" button remains available to retry.
    } finally {
      setGitHubStatus("idle");
    }
  }

  async function handleConnectGitHub() {
    if (!user) return;
    setGitHubStatus("connecting");
    setGitHubErrorMessage(null);
    try {
      const externalAccount = await createExternalAccountWithReverification({
        strategy: "oauth_github",
        redirectUrl: window.location.href,
      });
      const redirectUrl = externalAccount?.verification?.externalVerificationRedirectURL;
      if (redirectUrl) {
        window.location.href = redirectUrl.toString();
      } else {
        setGitHubStatus("idle");
      }
    } catch (err) {
      if (isReverificationCancelledError(err)) {
        setGitHubErrorMessage("Verification was cancelled — try again to connect GitHub.");
      } else {
        setGitHubErrorMessage("Couldn't start the GitHub connection. Try again.");
      }
      setGitHubStatus("idle");
    }
  }

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev));
  }

  function updateProofLink(index: number, field: keyof ProofLink, value: string) {
    setForm((prev) => {
      if (!prev) return prev;
      const proofLinks = prev.proofLinks.map((link, i) =>
        i === index ? { ...link, [field]: value } : link
      );
      return { ...prev, proofLinks };
    });
  }

  function addProofLink() {
    setForm((prev) =>
      prev ? { ...prev, proofLinks: [...prev.proofLinks, { label: "", url: "" }] } : prev
    );
  }

  function removeProofLink(index: number) {
    setForm((prev) =>
      prev ? { ...prev, proofLinks: prev.proofLinks.filter((_, i) => i !== index) } : prev
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form) return;

    setStatus("saving");
    setErrorMessage(null);

    try {
      await request("/api/portfolio/me", {
        method: "PUT",
        body: JSON.stringify({
          headline: form.headline,
          displayName: form.displayName,
          positioningLine: form.positioningLine,
          photoUrl: form.photoUrl || null,
          currentRole: form.currentRole || null,
          yearsExperience: form.yearsExperience || null,
          coreTechnologies: form.coreTechnologies
            .split(",")
            .map((t) => t.trim())
            .filter(Boolean),
          linkedInUrl: form.linkedInUrl || null,
          gitHubUrl: form.gitHubUrl || null,
          cvFileUrl: form.cvFileUrl || null,
          discipline: form.discipline,
          proofLinks: form.proofLinks
            .map((l) => ({ label: l.label.trim(), url: l.url.trim() }))
            .filter((l) => l.label || l.url),
          visibility: form.visibility,
          discoverable: form.discoverable,
          notificationEmail: form.notificationEmail || null,
        }),
      });
      setStatus("saved");
    } catch {
      setErrorMessage("Couldn't save — check that the API is running.");
      setStatus("error");
    }
  }

  if (status === "loading") {
    return <p className="text-sm text-zinc-500">Loading your profile…</p>;
  }

  if (!form) {
    return <p className="text-sm text-red-600">Couldn&apos;t load your profile — check that the API is running.</p>;
  }

  return (
    <div className="max-w-2xl">
      <h1 className="text-2xl font-semibold tracking-tight text-zinc-900">Profile</h1>
      {slug && <p className="mt-1 text-sm text-zinc-500">Public URL: devlog.app/{slug}</p>}

      <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-5">
        <Field label="Display name" hint="Shown as the page title on your Home page.">
          <input
            type="text"
            required
            value={form.displayName}
            onChange={(e) => update("displayName", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Positioning line" hint="One-line hero statement — not a generic 'About me.'">
          <textarea
            rows={2}
            value={form.positioningLine}
            onChange={(e) => update("positioningLine", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Current role" hint="Leave blank to show 'Actively building — open to opportunities.'">
          <input
            type="text"
            value={form.currentRole}
            onChange={(e) => update("currentRole", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Headline" hint="Internal label — not currently shown on the public page.">
          <input
            type="text"
            value={form.headline}
            onChange={(e) => update("headline", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Discipline" hint="Picks the right copy for a few fields below — no other effect.">
          <select
            value={form.discipline}
            onChange={(e) => update("discipline", e.target.value as Discipline)}
            className={inputClass}
          >
            {Object.entries(disciplineDisplayName).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </Field>

        <Field
          label={disciplineLabels[form.discipline].coreTechnologiesLabel}
          hint={`${disciplineLabels[form.discipline].coreTechnologiesHint} Comma-separated.`}
        >
          <input
            type="text"
            value={form.coreTechnologies}
            onChange={(e) => update("coreTechnologies", e.target.value)}
            className={inputClass}
          />
        </Field>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-zinc-700">GitHub</label>
            {form.gitHubVerified ? (
              <div className="mt-1 flex items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-sm text-zinc-900">
                <span className="truncate">{form.gitHubUrl}</span>
                <span className="shrink-0 rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-medium text-emerald-700">
                  Verified
                </span>
              </div>
            ) : (
              <button
                type="button"
                onClick={handleConnectGitHub}
                disabled={gitHubStatus !== "idle"}
                className="mt-1 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm font-medium text-zinc-900 hover:bg-zinc-50 disabled:opacity-50"
              >
                {gitHubStatus === "connecting" ? "Redirecting to GitHub…" : gitHubStatus === "syncing" ? "Confirming…" : "Connect GitHub"}
              </button>
            )}
            <span className="mt-1 block text-xs font-normal text-zinc-400">
              {form.gitHubVerified
                ? "Sourced from your connected GitHub account — disconnect it in Clerk to change this."
                : "Connects your real GitHub account so the link on your public page is verified, not just typed."}
            </span>
            {gitHubErrorMessage && <p className="mt-1 text-xs text-red-600">{gitHubErrorMessage}</p>}
          </div>
          <Field label="LinkedIn URL">
            <input
              type="url"
              value={form.linkedInUrl}
              onChange={(e) => update("linkedInUrl", e.target.value)}
              className={inputClass}
            />
          </Field>
        </div>

        <Field label="CV file URL" hint="A link to a hosted PDF — no file upload yet.">
          <input
            type="url"
            value={form.cvFileUrl}
            onChange={(e) => update("cvFileUrl", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Photo URL" hint="A link to a hosted image — no file upload yet.">
          <input
            type="url"
            value={form.photoUrl}
            onChange={(e) => update("photoUrl", e.target.value)}
            className={inputClass}
          />
        </Field>

        <div>
          <label className="block text-sm font-medium text-zinc-700">
            {disciplineLabels[form.discipline].proofLinksLabel}
            <span className="mt-1 block text-xs font-normal text-zinc-400">
              Optional extra links — a portfolio site, published articles, certifications.
            </span>
          </label>
          <div className="mt-2 flex flex-col gap-2">
            {form.proofLinks.map((link, i) => (
              <div key={i} className="flex items-center gap-2">
                <input
                  type="text"
                  placeholder="Label"
                  value={link.label}
                  onChange={(e) => updateProofLink(i, "label", e.target.value)}
                  className={`${inputClass} mt-0 w-32`}
                />
                <input
                  type="url"
                  placeholder="https://…"
                  value={link.url}
                  onChange={(e) => updateProofLink(i, "url", e.target.value)}
                  className={`${inputClass} mt-0 flex-1`}
                />
                <button
                  type="button"
                  onClick={() => removeProofLink(i)}
                  className="shrink-0 rounded-lg border border-zinc-200 px-2.5 py-2 text-xs font-medium text-zinc-500 hover:bg-zinc-50"
                >
                  Remove
                </button>
              </div>
            ))}
            <button
              type="button"
              onClick={addProofLink}
              className="self-start rounded-lg border border-zinc-200 px-3 py-1.5 text-xs font-medium text-zinc-600 hover:bg-zinc-50"
            >
              + Add link
            </button>
          </div>
        </div>

        <Field
          label="Notification email"
          hint="Where you're emailed when a verified interview request comes in."
        >
          <input
            type="email"
            value={form.notificationEmail}
            onChange={(e) => update("notificationEmail", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Visibility">
          <select
            value={form.visibility}
            onChange={(e) => update("visibility", e.target.value as PortfolioVisibility)}
            className={inputClass}
          >
            <option value="Unlisted">Unlisted — reachable by direct link only</option>
            <option value="Public">Public</option>
            <option value="Paused">Paused — hidden from everyone, including your own link</option>
          </select>
        </Field>

        {errorMessage && <p className="text-sm text-red-600">{errorMessage}</p>}

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={status === "saving"}
            className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {status === "saving" ? "Saving…" : "Save changes"}
          </button>
          {status === "saved" && <span className="text-sm text-emerald-600">Saved.</span>}
        </div>
      </form>
    </div>
  );
}

const inputClass =
  "mt-1 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-indigo-400";

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <label className="block text-sm font-medium text-zinc-700">
      {label}
      {children}
      {hint && <span className="mt-1 block text-xs font-normal text-zinc-400">{hint}</span>}
    </label>
  );
}
